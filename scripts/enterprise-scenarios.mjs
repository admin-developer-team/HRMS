import fs from 'node:fs';
import path from 'node:path';
import { randomBytes } from 'node:crypto';
import assert from 'node:assert/strict';

// Writes only to a newly provisioned QA tenant. Credentials stay in ignored artifacts.
const base = process.env.HRMS_TEST_URL ?? 'http://localhost:5208/api/v1';
const secretsPath = path.join(process.env.APPDATA, 'Microsoft', 'UserSecrets', 'hrms-backend-local-development', 'secrets.json');
const settings = JSON.parse(fs.readFileSync(secretsPath, 'utf8').replace(/^\uFEFF/, ''));
const setting = (section, key) => settings[`${section}:${key}`] ?? settings[section]?.[key];
const slug = `enterprise-qa-${new Date().toISOString().replace(/\D/g, '').slice(0, 14)}`;
const password = `QA-${randomBytes(15).toString('base64url')}!7`;
const results = [];
const fixture = { slug, password, base, accounts: {}, employees: {}, projects: {}, items: [] };
fs.mkdirSync('artifacts', { recursive: true });
const save = () => fs.writeFileSync('artifacts/enterprise-qa.local', JSON.stringify(fixture, null, 2));
async function api(session, method, route, data, expected, headers = {}) {
  const response = await fetch(`${base}${route}`, { method, headers: { 'Content-Type': 'application/json', ...(session ? {Authorization: `Bearer ${session.accessToken}`} : {}), ...headers }, ...(data === undefined ? {} : {body: JSON.stringify(data)}) });
  const raw = await response.text();
  const body = raw ? JSON.parse(raw) : null;
  if (expected !== undefined) assert.equal(response.status, expected, `${method} ${route}: ${body?.detail ?? response.status}`);
  else assert.ok(response.ok, `${method} ${route}: ${response.status} ${body?.detail ?? JSON.stringify(body)}`);
  return body;
}
const get = (s, p) => api(s, 'GET', p);
const post = (s, p, d) => api(s, 'POST', p, d);
const put = (s, p, d) => api(s, 'PUT', p, d);
const login = (email, company = slug, secret = password) => post(null, '/auth/login', {tenantSlug: company, email, password: secret});
async function test(name, action) {
  try { await action(); results.push({name, passed:true}); console.log(`PASS ${name}`); }
  catch (e) { results.push({name, passed:false, error:e.message}); console.log(`FAIL ${name}: ${e.message}`); }
}
let admin, platform;
try {
  platform = await login(setting('Bootstrap', 'PlatformAdminEmail'), 'platform', setting('Bootstrap', 'PlatformAdminPassword'));
  const company = await post(platform, '/platform/tenants', {name:'AsterWorks Enterprise QA', slug, adminName:'QA Company Administrator', adminEmail:'admin@asterworks.example', adminPassword:password, defaultCurrency:'INR', timeZone:'Asia/Kolkata', employeeLimit:250});
  fixture.tenantId = company.id;
  admin = await login('admin@asterworks.example'); fixture.accounts.admin = 'admin@asterworks.example'; save();
  const roles = await get(admin, '/identity/roles');
  const roleIds = names => names.map(name => { const role = roles.find(r => r.name === name); assert.ok(role, `Missing role ${name}`); return role.id; });
  const department = await post(admin, '/organization/departments', {name:'Product Engineering', code:'ENG'});
  const location = await post(admin, '/organization/locations', {name:'Bengaluru Office', code:'BLR', city:'Bengaluru', countryCode:'IN'});
  const people = [
    ['manager','Rohan','Mehta',['People Manager','Work Coordinator']],
    ['employee','Asha','Rao',['Employee Self-Service','Work Contributor']],
    ['coordinator','Leena','Shah',['Employee Self-Service','Work Coordinator']],
    ['developer','Vikram','Das',['Employee Self-Service','Work Contributor']],
    ['outsider','Nisha','Patel',['Employee Self-Service','Work Contributor']],
    ['hr','Meera','Iyer',['HR Administrator','Employee Self-Service']],
    ['payroll','Arjun','Sen',['Payroll Administrator','Employee Self-Service']],
  ];
  const sessions = {};
  for (const [key,firstName,lastName,names] of people) {
    const email = `${key}@asterworks.example`;
    const employee = await post(admin, '/employees', {employeeNumber:`AST-${String(Object.keys(fixture.employees).length + 1).padStart(3,'0')}`, firstName,lastName,workEmail:email,hireDate:'2025-01-06',departmentId:department.id,locationId:location.id,managerId:['employee','developer','coordinator'].includes(key)?fixture.employees.manager.id:null,baseSalary:75000,salaryCurrency:'INR'});
    fixture.employees[key] = employee; fixture.accounts[key] = email;
    await post(admin, `/identity/employees/${employee.id}/account`, {password,roleIds:roleIds(names)});
    sessions[key] = await login(email);
  }
  save();
  await test('Seven role-specific accounts authenticate', async () => assert.equal(Object.keys(sessions).length,7));
  await test('Employee cannot read HR employee register', () => api(sessions.employee,'GET','/employees',undefined,403));
  await test('Employee cannot access payroll', () => api(sessions.employee,'GET','/payroll/runs',undefined,403));
  await test('Payroll can read payroll but cannot administer roles', async () => { await get(sessions.payroll,'/payroll/runs'); await api(sessions.payroll,'GET','/identity/roles',undefined,403); });
  await test('HR cannot grant wildcard access', () => api(sessions.hr,'POST','/identity/roles',{name:'Escalation blocked',permissions:['*']},403));
  await test('HR cannot reset tenant administrator password', () => api(sessions.hr,'PUT',`/identity/users/${admin.user.id}/password`,{password:'Never-applied-secret-123!'},403));
  await test('Tenant header mismatch is rejected', () => api(sessions.employee,'GET','/me/profile',undefined,403,{'X-Tenant-ID':platform.user.tenantId}));
  await test('Manager sees exactly three direct reports', async () => assert.equal((await get(sessions.manager,'/me/team')).length,3));
  await put(admin,'/attendance/policy',{officeStartsAt:'09:00:00',officeEndsAt:'17:00:00',lateGraceMinutes:10,earlyDepartureGraceMinutes:10,workingDays:['Monday','Tuesday','Wednesday','Thursday','Friday'],requireLocationCapture:false,version:0});
  let attendance;
  await test('Offset-aware manual attendance persists in PostgreSQL', async () => {
    attendance = await post(admin,'/attendance/clock-in',{employeeId:fixture.employees.employee.id,timestamp:'2026-08-03T09:00:00+05:30',notes:'Verified office sign-in during QA'});
    const closed = await post(admin,'/attendance/clock-out',{employeeId:fixture.employees.employee.id,timestamp:'2026-08-03T17:00:00+05:30',notes:'Verified office sign-out during QA'});
    assert.equal(closed.workHours,8);
  });
  await test('Overlapping historical check-in is rejected', () => api(admin,'POST','/attendance/clock-in',{employeeId:fixture.employees.employee.id,timestamp:'2026-08-03T12:00:00+05:30',notes:'Attempting overlap for regression'},400));
  let correction;
  await test('Employee submits missing-day correction', async () => { correction = await post(sessions.employee,'/attendance-corrections',{workDate:'2026-08-04',requestedClockIn:'2026-08-04T09:00:00+05:30',requestedClockOut:'2026-08-04T17:00:00+05:30',reason:'Missed punches during a scheduled client visit'}); assert.equal(correction.status,'Pending'); });
  await test('Employee cannot self-approve correction', () => api(sessions.employee,'PUT',`/attendance-corrections/${correction.id}/review`,{approve:true,version:correction.version},403));
  await test('Unrelated employee cannot review correction', () => api(sessions.outsider,'PUT',`/attendance-corrections/${correction.id}/review`,{approve:true,version:correction.version},403));
  await test('Direct manager approval creates exactly eight attendance hours', async () => {
    const approved=await put(sessions.manager,`/attendance-corrections/${correction.id}/review`,{approve:true,comment:'Client visit verified',version:correction.version});
    assert.equal(approved.status,'Approved');
    const report=await get(sessions.employee,'/me/attendance/report?from=2026-08-04&to=2026-08-04'); assert.equal(report.items[0].totalHours,8);
  });
  const open = await post(admin,'/attendance/clock-in',{employeeId:fixture.employees.employee.id,timestamp:'2026-08-05T09:00:00+05:30',notes:'Missed check-out demonstration'});
  await test('Stale open attendance does not inflate worked hours', async () => { const report=await get(sessions.employee,'/me/attendance/report?from=2026-08-05&to=2026-08-05'); assert.equal(report.items[0].totalHours,0); assert.match(report.items[0].status,/correction required/); });
  await post(sessions.employee,'/attendance-corrections',{attendanceRecordId:open.id,workDate:'2026-08-05',requestedClockIn:'2026-08-05T09:00:00+05:30',requestedClockOut:'2026-08-05T17:00:00+05:30',reason:'Forgot to check out after office network outage'});
  const leaveTypes=await get(sessions.employee,'/me/leave-types');
  const annual=leaveTypes.find(x=>x.code==='ANNUAL');
  await test('Leave rejects under-reported working-day count',()=>api(sessions.employee,'POST','/me/leave',{leaveTypeId:annual.id,startsOn:'2026-10-05',endsOn:'2026-10-09',days:1,reason:'Planned family holiday'},400));
  await test('Leave cancellation restores pending balance',async()=>{
    const before=(await get(sessions.employee,'/me/leave-balances?year=2026')).find(x=>x.leaveTypeId===annual.id).available;
    const request=await post(sessions.employee,'/me/leave',{leaveTypeId:annual.id,startsOn:'2026-10-05',endsOn:'2026-10-09',days:5,reason:'Planned family holiday'});
    await put(sessions.employee,`/me/leave/${request.id}/cancel?version=${request.version}`,{});
    assert.equal((await get(sessions.employee,'/me/leave-balances?year=2026')).find(x=>x.leaveTypeId===annual.id).available,before);
  });
  await post(sessions.developer,'/me/leave',{leaveTypeId:annual.id,startsOn:'2026-10-12',endsOn:'2026-10-13',days:2,reason:'Planned personal leave'});
  const project=await post(admin,'/work/projects',{key:'PEOPLE',name:'Employee Experience Platform',description:'Simplify onboarding, attendance and daily delivery for AsterWorks.',leadEmployeeId:fixture.employees.manager.id});
  fixture.projects.people=project;
  await put(admin,`/work/projects/${project.id}/members`,['manager','employee','coordinator','developer'].map(key=>({employeeId:fixture.employees[key].id,canCreateItems:true,canAssignItems:key==='manager'||key==='coordinator',canTransitionItems:true,canLogWork:true,canViewAllWorklogs:key==='manager'||key==='coordinator'})));
  await test('Non-member cannot see project or sprint scope',async()=>{ assert.equal((await get(sessions.outsider,'/work/projects')).length,0); await api(sessions.outsider,'GET',`/work/projects/${project.id}/sprints`,undefined,403); });
  const itemRequest=(summary,type='Task')=>({projectId:project.id,type,summary,description:'Acceptance criteria: available on desktop and mobile, permission checked, and verified with realistic HR workflows.',assigneeEmployeeIds:[fixture.employees.employee.id],reporterEmployeeId:fixture.employees.coordinator.id,priority:'High',dueDate:'2026-09-18',originalEstimateMinutes:240,storyPoints:3,labels:['employee-experience','qa']});
  let primary=await post(sessions.coordinator,'/work/items',itemRequest('Make attendance corrections easy to review','Story'));
  fixture.primaryItemId=primary.id;
  const themes=['New starter checklist','Manager approval inbox','Attendance exception report','Leave balance clarity','Accessible mobile navigation','Department directory','Payroll export review','Document expiry reminder','Project time report','Asset return checklist'];
  for(let i=0;i<124;i++){
    const item=await post(admin,'/work/items',{...itemRequest(`${themes[i%themes.length]} · ${Math.floor(i/themes.length)+1}`,i%7===0?'Bug':'Task'),priority:['Medium','High','Low','Critical'][i%4],assigneeEmployeeIds:[fixture.employees[['employee','developer','coordinator'][i%3]].id],labels:[i%2?'frontend':'backend','qa']});
    fixture.items.push(item);
  }
  save();
  await test('125 tickets are available beyond the first page',async()=>{ const page=await get(sessions.employee,`/work/items?projectId=${project.id}&page=5&pageSize=30`); assert.equal(page.total,125); assert.equal(page.items.length,5); });
  let sprint=await post(sessions.coordinator,`/work/projects/${project.id}/sprints`,{name:'September · Employee experience',goal:'Deliver a simpler daily workspace and auditable attendance corrections.',startsOn:'2026-09-07',endsOn:'2026-09-20'});
  await test('Contributor cannot plan sprint scope',()=>api(sessions.employee,'PUT',`/work/items/${primary.id}/sprint`,{sprintId:sprint.id,version:primary.version},403));
  primary=await put(sessions.coordinator,`/work/items/${primary.id}/sprint`,{sprintId:sprint.id,version:primary.version});
  sprint=await put(sessions.coordinator,`/work/sprints/${sprint.id}/status`,{status:'Active',version:sprint.version});
  fixture.sprintId=sprint.id;
  await test('Sprint filtering returns only assigned sprint work',async()=>{ const page=await get(sessions.employee,`/work/items?projectId=${project.id}&sprintId=${sprint.id}`); assert.equal(page.total,1); assert.equal(page.items[0].id,primary.id); });
  await test('Invalid board transition is rejected',()=>api(sessions.coordinator,'PUT',`/work/items/${primary.id}/transition`,{status:'Done',version:primary.version},400));
  primary=await put(sessions.coordinator,`/work/items/${primary.id}/transition`,{status:'ToDo',version:primary.version});
  primary=await put(sessions.coordinator,`/work/items/${primary.id}/transition`,{status:'InProgress',version:primary.version});
  const child=await post(sessions.coordinator,'/work/items',{...itemRequest('Verify correction approvals on mobile','Subtask'),parentId:primary.id});
  await test('Parent completion is blocked by unfinished subtask',()=>api(sessions.coordinator,'PUT',`/work/items/${primary.id}/transition`,{status:'Done',version:primary.version},400));
  await test('Employee logs time and gets scoped report',async()=>{ await post(sessions.employee,`/work/items/${primary.id}/worklogs`,{workDate:'2026-09-04',minutes:90,description:'Reviewed attendance edge cases'}); const report=await get(sessions.employee,`/work/reports/time?from=2026-09-01&to=2026-09-07&projectId=${project.id}`); assert.equal(report.totalMinutes,90); });
  await post(sessions.employee,`/work/items/${primary.id}/comments`,{body:'The missing check-out scenario is reproduced. Correction review is ready for manager testing.'});
  for(let i=0;i<12;i++){
    let item=fixture.items[i];
    item=await put(sessions.coordinator,`/work/items/${item.id}/transition`,{status:'ToDo',version:item.version});
    if(i>=4) item=await put(sessions.coordinator,`/work/items/${item.id}/transition`,{status:'InProgress',version:item.version});
    if(i>=8) item=await put(sessions.coordinator,`/work/items/${item.id}/transition`,{status:i>=10?'Done':'InReview',version:item.version});
  }
  await test('Role removal takes effect on an existing access token',async()=>{
    const account=(await get(admin,'/identity/users?pageSize=100')).items.find(x=>x.email===fixture.accounts.outsider);
    await put(admin,`/identity/users/${account.id}/roles`,{roleIds:roleIds(['Employee Self-Service']),version:account.version});
    await api(sessions.outsider,'GET','/work/projects',undefined,403);
    const updated=(await get(admin,'/identity/users?pageSize=100')).items.find(x=>x.id===account.id);
    await put(admin,`/identity/users/${account.id}/roles`,{roleIds:roleIds(['Employee Self-Service','Work Contributor']),version:updated.version});
    await get(sessions.outsider,'/work/projects');
  });
  await test('Logout immediately revokes existing access token',async()=>{
    const temporary=await login(fixture.accounts.outsider); await post(temporary,'/auth/revoke',{refreshToken:temporary.refreshToken});
    await api(temporary,'GET','/me/profile',undefined,401);
  });
  await test('Attendance report remains isolated to the requesting employee',async()=>{ const report=await get(sessions.developer,'/me/attendance?from=2026-08-01&to=2026-08-31'); assert.equal(report.total,0); });
  console.log(`QA tenant ready: ${slug}`);
} catch(e) {
  results.push({name:'Scenario setup and completion',passed:false,error:e.message}); console.error(e.message);
} finally {
  save();
  const report={createdAt:new Date().toISOString(),tenantSlug:slug,results,passed:results.filter(x=>x.passed).length,failed:results.filter(x=>!x.passed).length};
  fs.writeFileSync('artifacts/enterprise-scenarios.json',JSON.stringify(report,null,2));
  console.log(`${report.passed} passed; ${report.failed} failed. Report: artifacts/enterprise-scenarios.json`);
  if(report.failed) process.exitCode=1;
}

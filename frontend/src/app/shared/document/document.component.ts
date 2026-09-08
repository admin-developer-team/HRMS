import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import {
  DomSanitizer,
  SafeHtml,
  SafeResourceUrl,
} from '@angular/platform-browser';

import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';

import { renderAsync } from 'docx-preview';
import JSZip, { type JSZipObject } from 'jszip';
import * as XLSX from 'xlsx';

import {
  Observable,
  concatMap,
  finalize,
  from,
  of,
  tap,
  toArray,
} from 'rxjs';

import { DocumentService } from '../../core/document.service';
import {
  DocumentOwnerType,
  StoredDocument,
} from '../../core/models';
import { ToastService } from '../../core/toast.service';
import heic2any from 'heic2any';
interface PendingDocument {
  file: File;
  url?: string;
}

type PreviewKind =
  | 'image'
  | 'pdf'
  | 'video'
  | 'audio'
  | 'excel'
  | 'docx'
  | 'text'
  | 'email'
  | 'archive'
  | 'unsupported';

interface PreviewState {
  document: StoredDocument;
  name: string;
  extension: string;
  contentType: string;
  kind: PreviewKind;
  rawUrl?: string;
  resourceUrl?: SafeResourceUrl;
  note?: string;
}

interface ExcelSheetPreview {
  name: string;
  html: SafeHtml;
}

interface ArchiveEntry {
  name: string;
  size: number;
  isDir: boolean;
}

interface EmailPreview {
  subject: string;
  from: string;
  to: string;
  cc: string;
  date: string;
  body: string;
  htmlBody: string;
  isHtml: boolean;
  attachments: string[];
}

interface TiffIfd {
  width: number;
  height: number;
  [key: string]: unknown;
}

interface UtifModule {
  decode(buffer: ArrayBuffer): TiffIfd[];
  decodeImage(buffer: ArrayBuffer, ifd: TiffIfd): void;
  toRGBA8(ifd: TiffIfd): Uint8Array;
}

@Component({
  selector: 'app-document',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './document.component.html',
  styleUrl: './document.component.scss',
})
export class DocumentComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) ownerType!: DocumentOwnerType;
  @Input() ownerId = '';
  @Input() category = 'attachment';
  @Input() label = 'Documents';
  @Input() readonly = false;
  @Input() replaceMode = false;
  @Input() maxFiles = 10;
  /** Set to a positive value to enforce a client-side limit; zero means unlimited. */
  @Input() maxSizeMb = 0;
  @Input() allowedExtensions?: string[];
  @Input() showUploadButton = true;
  @Input() compact = false;

  @Output() documentsChanged = new EventEmitter<StoredDocument[]>();

  @ViewChild('docxContainer')
  private docxContainer?: ElementRef<HTMLDivElement>;

  private readonly service = inject(DocumentService);
  private readonly toast = inject(ToastService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly documents = signal<StoredDocument[]>([]);
  readonly pending = signal<PendingDocument[]>([]);
  readonly busy = signal(false);
  readonly dragActive = signal(false);

  readonly preview = signal<PreviewState | null>(null);
  readonly previewBusy = signal(false);

  readonly excelSheets = signal<ExcelSheetPreview[]>([]);
  readonly activeSheetIndex = signal(0);

  readonly textLines = signal<{ n: number; text: string }[]>([]);

  readonly archiveEntries = signal<ArchiveEntry[]>([]);

  readonly emailPreview = signal<EmailPreview | null>(null);
  readonly emailHtmlUrl = signal<SafeResourceUrl | null>(null);

  readonly zoomLevel = signal(1);
  readonly zoomMin = 0.25;
  readonly zoomMax = 4;
  readonly zoomStep = 0.1;

  private previewBlob: Blob | null = null;
  private emailObjectUrl = '';
  private lastWheelZoomTime = 0;

  private readonly wheelZoomThrottleMs = 60;

  static readonly extensionGroups = {
    image: [
      'jpg',
      'jpeg',
      'jpe',
      'jpd',
      'jfif',
      'png',
      'gif',
      'webp',
      'bmp',
      'svg',
      'avif',
      'ico',
      'heic',
      'heif',
      'tif',
      'tiff',
    ],

    pdf: [
      'pdf',
    ],

    word: [
      'doc',
      'docx',
      'dotx',
      'rtf',
      'odt',
    ],

    sheet: [
      'xls',
      'xlsx',
      'xlsm',
      'xlsb',
      'xltx',
      'csv',
      'ods',
    ],

    presentation: [
      'ppt',
      'pptx',
      'odp',
    ],

    text: [
      'txt',
      'json',
      'xml',
      'log',
      'md',
      'yml',
      'yaml',
      'ini',
      'sql',
      'js',
      'ts',
      'css',
      'html',
      'htm',
      'sh',
      'conf',
    ],

    archive: [
      'zip',
      'rar',
      '7z',
    ],

    audio: [
      'mp3',
      'wav',
      'ogg',
      'm4a',
      'aac',
    ],

    video: [
      'mp4',
      'webm',
      'mov',
    ],

    email: [
      'eml',
      'msg',
    ],
  } as const;

  static readonly allExtensions = Array.from(
    new Set(
      Object.values(DocumentComponent.extensionGroups).flat(),
    ),
  );

  private static readonly mimeMap: Record<string, string> = {
    pdf: 'application/pdf',

    png: 'image/png',
    jpg: 'image/jpeg',
    jpeg: 'image/jpeg',
    jpe: 'image/jpeg',
    jpd: 'image/jpeg',
    jfif: 'image/jpeg',
    gif: 'image/gif',
    bmp: 'image/bmp',
    webp: 'image/webp',
    svg: 'image/svg+xml',
    avif: 'image/avif',
    ico: 'image/x-icon',
    heic: 'image/heic',
    heif: 'image/heif',
    tif: 'image/tiff',
    tiff: 'image/tiff',

    doc: 'application/msword',
    docx:
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    dotx:
      'application/vnd.openxmlformats-officedocument.wordprocessingml.template',
    rtf: 'application/rtf',
    odt: 'application/vnd.oasis.opendocument.text',

    xls: 'application/vnd.ms-excel',
    xlsx:
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    xlsm:
      'application/vnd.ms-excel.sheet.macroEnabled.12',
    xlsb:
      'application/vnd.ms-excel.sheet.binary.macroEnabled.12',
    xltx:
      'application/vnd.openxmlformats-officedocument.spreadsheetml.template',
    csv: 'text/csv',
    ods: 'application/vnd.oasis.opendocument.spreadsheet',

    ppt: 'application/vnd.ms-powerpoint',
    pptx:
      'application/vnd.openxmlformats-officedocument.presentationml.presentation',
    odp: 'application/vnd.oasis.opendocument.presentation',

    txt: 'text/plain',
    json: 'application/json',
    xml: 'application/xml',
    log: 'text/plain',
    md: 'text/markdown',
    yml: 'text/yaml',
    yaml: 'text/yaml',
    ini: 'text/plain',
    sql: 'text/plain',
    js: 'text/javascript',
    ts: 'text/typescript',
    css: 'text/css',
    html: 'text/html',
    htm: 'text/html',
    sh: 'text/plain',
    conf: 'text/plain',

    mp4: 'video/mp4',
    webm: 'video/webm',
    mov: 'video/quicktime',

    mp3: 'audio/mpeg',
    wav: 'audio/wav',
    ogg: 'audio/ogg',
    m4a: 'audio/mp4',
    aac: 'audio/aac',

    eml: 'message/rfc822',
    msg: 'application/vnd.ms-outlook',

    zip: 'application/zip',
    rar: 'application/vnd.rar',
    '7z': 'application/x-7z-compressed',
  };

  private readonly browserImageExtensions = new Set([
    'jpg',
    'jpeg',
    'jpe',
    'jpd',
    'jfif',
    'png',
    'gif',
    'webp',
    'bmp',
    'svg',
    'avif',
    'ico',
  ]);

  private readonly spreadsheetExtensions = new Set([
    'xls',
    'xlsx',
    'xlsm',
    'xlsb',
    'xltx',
    'csv',
    'ods',
  ]);

  private readonly docxExtensions = new Set([
    'docx',
    'dotx',
  ]);

  private readonly textPreviewExtensions = new Set([
    'txt',
    'json',
    'xml',
    'log',
    'md',
    'yml',
    'yaml',
    'ini',
    'sql',
    'js',
    'ts',
    'css',
    'html',
    'htm',
    'sh',
    'conf',
  ]);

  private readonly videoExtensions = new Set([
    'mp4',
    'webm',
    'mov',
  ]);

  private readonly audioExtensions = new Set([
    'mp3',
    'wav',
    'ogg',
    'm4a',
    'aac',
  ]);

  get accept(): string {
    const extensions =
      this.allowedExtensions?.length
        ? this.allowedExtensions
        : DocumentComponent.allExtensions;

    const values = new Set<string>();

    for (const value of extensions) {
      const extension = this.normalizeExtension(value);

      if (!extension) {
        continue;
      }

      values.add(`.${extension}`);

      const mime = DocumentComponent.mimeMap[extension];

      if (mime) {
        values.add(mime);
      }
    }

    return Array.from(values).join(',');
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (
      changes['ownerId'] ||
      changes['ownerType'] ||
      changes['category']
    ) {
      this.closePreview();

      if (this.ownerId) {
        this.load();
      } else {
        this.documents.set([]);
      }
    }
  }

  ngOnDestroy(): void {
    this.pending().forEach((item) => {
      if (item.url) {
        URL.revokeObjectURL(item.url);
      }
    });

    this.closePreview();
  }

  selectFiles(event: Event): void {
    const input = event.target as HTMLInputElement;

    this.addFiles(
      Array.from(input.files ?? []),
    );

    input.value = '';
  }

  drop(event: DragEvent): void {
    event.preventDefault();

    this.dragActive.set(false);

    this.addFiles(
      Array.from(event.dataTransfer?.files ?? []),
    );
  }

  @HostListener('paste', ['$event'])
  paste(event: ClipboardEvent): void {
    if (this.readonly) {
      return;
    }

    const files = Array.from(
      event.clipboardData?.files ?? [],
    );

    if (!files.length) {
      return;
    }

    event.preventDefault();

    this.addFiles(files);
  }

  @HostListener('document:keydown.escape')
  closeWithEscape(): void {
    if (this.preview()) {
      this.closePreview();
    }
  }

  removePending(index: number): void {
    const item = this.pending()[index];

    if (item?.url) {
      URL.revokeObjectURL(item.url);
    }

    this.pending.update((items) =>
      items.filter(
        (_, itemIndex) => itemIndex !== index,
      ),
    );
  }

  saveDocuments(
    ownerId = this.ownerId,
  ): Observable<StoredDocument[]> {
    const files = this.pending().map(
      (item) => item.file,
    );

    if (!files.length) {
      return of([]);
    }

    if (!ownerId) {
      throw new Error(
        'A document owner is required before upload.',
      );
    }

    this.busy.set(true);

    return from(files).pipe(
      concatMap((file) =>
        this.service.upload(
          this.ownerType,
          ownerId,
          this.category,
          file,
          this.replaceMode,
        ),
      ),

      toArray(),

      tap((uploaded) => {
        this.pending().forEach((item) => {
          if (item.url) {
            URL.revokeObjectURL(item.url);
          }
        });

        this.pending.set([]);

        this.documents.update((items) =>
          this.replaceMode
            ? uploaded
            : [...uploaded, ...items],
        );

        this.documentsChanged.emit(
          this.documents(),
        );
      }),

      finalize(() => {
        this.busy.set(false);
      }),
    );
  }

  open(document: StoredDocument): void {
    if (this.busy()) {
      return;
    }

    this.clearPreviewData();

    this.busy.set(true);
    this.previewBusy.set(true);

    this.service.content(document.id).subscribe({
      next: (blob) => {
        void this.preparePreview(
          document,
          blob,
        )
          .catch(() => {
            this.toast.error(
              'Could not preview the document.',
            );

            this.setUnsupportedPreview(
              document,
              blob,
              'The file could not be rendered in the browser. You can still download it.',
            );
          })
          .finally(() => {
            this.previewBusy.set(false);
            this.busy.set(false);
          });
      },

      error: () => {
        this.previewBusy.set(false);
        this.busy.set(false);

        this.toast.error(
          'Could not open the document.',
        );
      },
    });
  }

  download(document: StoredDocument): void {
    this.service.content(document.id).subscribe({
      next: (blob) => {
        this.downloadBlob(
          blob,
          document.fileName,
        );
      },

      error: () => {
        this.toast.error(
          'Could not download the document.',
        );
      },
    });
  }

  downloadCurrent(): void {
    const current = this.preview();

    if (!current) {
      return;
    }

    if (this.previewBlob) {
      this.downloadBlob(
        this.previewBlob,
        current.name,
      );

      return;
    }

    this.download(current.document);
  }

  delete(document: StoredDocument): void {
    if (
      !confirm(
        `Delete ${document.fileName}?`,
      )
    ) {
      return;
    }

    this.busy.set(true);

    this.service
      .delete(document.id)
      .pipe(
        finalize(() =>
          this.busy.set(false),
        ),
      )
      .subscribe({
        next: () => {
          if (
            this.preview()?.document.id ===
            document.id
          ) {
            this.closePreview();
          }

          this.documents.update((items) =>
            items.filter(
              (item) =>
                item.id !== document.id,
            ),
          );

          this.documentsChanged.emit(
            this.documents(),
          );

          this.toast.success(
            'Document deleted.',
          );
        },

        error: () => {
          this.toast.error(
            'Could not delete the document.',
          );
        },
      });
  }

  closePreview(): void {
    this.clearPreviewData();

    this.previewBusy.set(false);
    this.zoomLevel.set(1);
  }

  zoomIn(): void {
    this.zoomLevel.update((value) =>
      Math.min(
        this.zoomMax,
        +(value + this.zoomStep).toFixed(2),
      ),
    );
  }

  zoomOut(): void {
    this.zoomLevel.update((value) =>
      Math.max(
        this.zoomMin,
        +(value - this.zoomStep).toFixed(2),
      ),
    );
  }

  resetZoom(): void {
    this.zoomLevel.set(1);
  }

  canZoom(kind: PreviewKind): boolean {
    return [
      'image',
      'excel',
      'docx',
      'text',
      'email',
    ].includes(kind);
  }

  onPreviewWheel(event: WheelEvent): void {
    const current = this.preview();

    if (
      !event.ctrlKey ||
      !current ||
      !this.canZoom(current.kind)
    ) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();

    const now = Date.now();

    if (
      now - this.lastWheelZoomTime <
      this.wheelZoomThrottleMs
    ) {
      return;
    }

    this.lastWheelZoomTime = now;

    if (event.deltaY < 0) {
      this.zoomIn();
    } else {
      this.zoomOut();
    }
  }

  isPreviewable(
    typeOrExtension: string,
  ): boolean {
    if (
      typeOrExtension.startsWith(
        'image/',
      ) ||
      typeOrExtension.startsWith(
        'video/',
      ) ||
      typeOrExtension.startsWith(
        'audio/',
      ) ||
      typeOrExtension ===
        'application/pdf' ||
      typeOrExtension.startsWith(
        'text/',
      )
    ) {
      return true;
    }

    const extension =
      this.normalizeExtension(
        typeOrExtension,
      );

    return (
      this.browserImageExtensions.has(
        extension,
      ) ||
      this.spreadsheetExtensions.has(
        extension,
      ) ||
      this.docxExtensions.has(
        extension,
      ) ||
      this.textPreviewExtensions.has(
        extension,
      ) ||
      this.videoExtensions.has(
        extension,
      ) ||
      this.audioExtensions.has(
        extension,
      ) ||
      [
        'pdf',
        'heic',
        'heif',
        'tif',
        'tiff',
        'eml',
        'zip',
      ].includes(extension)
    );
  }

  icon(extensionOrName: string): string {
    const extension = this.normalizeExtension(extensionOrName);
  
    if ([
      'jpg', 'jpeg', 'jpe', 'jpd', 'jfif',
      'png', 'gif', 'webp', 'bmp', 'svg',
      'avif', 'ico', 'heic', 'heif', 'tif', 'tiff'
    ].includes(extension)) {
      return 'image';
    }
  
    if (extension === 'pdf') {
      return 'picture_as_pdf';
    }
  
    if ([
      'xls', 'xlsx', 'xlsm', 'xlsb',
      'xltx', 'csv', 'ods'
    ].includes(extension)) {
      return 'table_chart';
    }
  
    if ([
      'doc', 'docx', 'dotx', 'rtf', 'odt'
    ].includes(extension)) {
      return 'article';
    }
  
    if ([
      'ppt', 'pptx', 'odp'
    ].includes(extension)) {
      return 'slideshow';
    }
  
    if ([
      'mp4', 'webm', 'mov'
    ].includes(extension)) {
      return 'movie';
    }
  
    if ([
      'mp3', 'wav', 'ogg', 'm4a', 'aac'
    ].includes(extension)) {
      return 'audio_file';
    }
  
    if ([
      'zip', 'rar', '7z'
    ].includes(extension)) {
      return 'folder_zip';
    }
  
    if ([
      'eml', 'msg'
    ].includes(extension)) {
      return 'mail';
    }
  
    if ([
      'txt', 'json', 'xml', 'log',
      'md', 'yml', 'yaml', 'ini',
      'sql', 'js', 'ts', 'css',
      'html', 'htm', 'sh', 'conf'
    ].includes(extension)) {
      return 'code';
    }
  
    return 'description';
  }
  
  iconColor(extensionOrName: string): string {
    const extension = this.normalizeExtension(extensionOrName);
  
    if (extension === 'pdf') {
      return '#dc2626';
    }
  
    if ([
      'xls', 'xlsx', 'xlsm', 'xlsb',
      'xltx', 'csv', 'ods'
    ].includes(extension)) {
      return '#16803c';
    }
  
    if ([
      'doc', 'docx', 'dotx', 'odt'
    ].includes(extension)) {
      return '#2563eb';
    }
  
    if ([
      'ppt', 'pptx', 'odp'
    ].includes(extension)) {
      return '#ea580c';
    }
  
    if ([
      'jpg', 'jpeg', 'jpe', 'jpd', 'jfif',
      'png', 'gif', 'webp', 'bmp',
      'svg', 'avif', 'ico',
      'heic', 'heif', 'tif', 'tiff'
    ].includes(extension)) {
      return '#7c3aed';
    }
  
    if ([
      'zip', 'rar', '7z'
    ].includes(extension)) {
      return '#9333ea';
    }
  
    if ([
      'mp4', 'webm', 'mov'
    ].includes(extension)) {
      return '#dc2626';
    }
  
    if ([
      'mp3', 'wav', 'ogg', 'm4a', 'aac'
    ].includes(extension)) {
      return '#d97706';
    }
  
    if ([
      'eml', 'msg'
    ].includes(extension)) {
      return '#f59e0b';
    }
  
    if ([
      'json', 'xml', 'yml', 'yaml',
      'js', 'ts', 'css', 'html',
      'htm', 'sql', 'sh'
    ].includes(extension)) {
      return '#0891b2';
    }
  
    return 'var(--ink-muted)';
  }

  formatSize(bytes: number): string {
    if (!bytes) {
      return '0 B';
    }

    if (bytes < 1024) {
      return `${bytes} B`;
    }

    if (bytes < 1024 * 1024) {
      return `${(
        bytes / 1024
      ).toFixed(1)} KB`;
    }

    if (
      bytes <
      1024 * 1024 * 1024
    ) {
      return `${(
        bytes /
        (1024 * 1024)
      ).toFixed(1)} MB`;
    }

    return `${(
      bytes /
      (1024 * 1024 * 1024)
    ).toFixed(1)} GB`;
  }

  private load(): void {
    this.busy.set(true);

    this.service
      .list(
        this.ownerType,
        this.ownerId,
        this.category,
      )
      .pipe(
        finalize(() =>
          this.busy.set(false),
        ),
      )
      .subscribe({
        next: (items) => {
          this.documents.set(items);
        },

        error: () => {
          this.toast.error(
            `Could not load ${this.label.toLowerCase()}.`,
          );
        },
      });
  }

  private addFiles(
    files: File[],
  ): void {
    if (
      this.readonly ||
      !files.length
    ) {
      return;
    }

    const allowed = new Set(
      (
        this.allowedExtensions?.length
          ? this.allowedExtensions
          : DocumentComponent.allExtensions
      ).map((value) =>
        this.normalizeExtension(value),
      ),
    );

    if (this.replaceMode) {
      this.pending().forEach((item) => {
        if (item.url) {
          URL.revokeObjectURL(
            item.url,
          );
        }
      });
    }

    const current = this.replaceMode
      ? []
      : this.pending();

    const existingCount =
      this.replaceMode
        ? 0
        : this.documents().length;

    const accepted: PendingDocument[] =
      [];

    for (const file of files) {
      const extension =
        this.normalizeExtension(
          file.name,
        );

      if (!allowed.has(extension)) {
        this.toast.error(
          `${file.name}: unsupported file type.`,
        );

        continue;
      }

      if (
        this.maxSizeMb > 0 &&
        file.size >
        this.maxSizeMb *
          1024 *
          1024
      ) {
        this.toast.error(
          `${file.name}: maximum size is ${this.maxSizeMb} MB.`,
        );

        continue;
      }

      if (
        !this.replaceMode &&
        existingCount +
          current.length +
          accepted.length >=
          this.maxFiles
      ) {
        this.toast.error(
          `A maximum of ${this.maxFiles} files is allowed.`,
        );

        break;
      }

      accepted.push({
        file,

        url:
          this.browserImageExtensions.has(
            extension,
          )
            ? URL.createObjectURL(
                file,
              )
            : undefined,
      });

      if (this.replaceMode) {
        break;
      }
    }

    this.pending.set(
      this.replaceMode
        ? accepted
        : [
            ...current,
            ...accepted,
          ],
    );

    if (
      this.ownerId &&
      accepted.length
    ) {
      this.saveDocuments().subscribe({
        error: () => {
          this.toast.error(
            'Document upload failed.',
          );
        },
      });
    }
  }

  private async preparePreview(
    document: StoredDocument,
    blob: Blob,
  ): Promise<void> {
    this.previewBlob = blob;
    this.zoomLevel.set(1);

    const extension =
      this.normalizeExtension(
        document.extension ||
          document.fileName,
      );

    const contentType =
      blob.type ||
      document.contentType ||
      DocumentComponent.mimeMap[
        extension
      ] ||
      'application/octet-stream';

    const state: PreviewState = {
      document,
      name: document.fileName,
      extension,
      contentType,
      kind: 'unsupported',
    };

    if (
      this.spreadsheetExtensions.has(
        extension,
      )
    ) {
      this.preview.set({
        ...state,
        kind: 'excel',
      });

      await this.nextRender();

      await this.renderExcelPreview(
        blob,
        extension === 'csv',
      );

      return;
    }

    if (
      this.docxExtensions.has(
        extension,
      )
    ) {
      this.preview.set({
        ...state,
        kind: 'docx',
      });

      await this.nextRender();

      await this.renderDocxPreview(
        blob,
      );

      return;
    }

    if (extension === 'doc') {
      this.preview.set({
        ...state,
        kind: 'unsupported',
        note:
          'Legacy .DOC files cannot be rendered reliably in a browser. Download the file to open it in Microsoft Word or another compatible application.',
      });

      return;
    }

    if (
      this.textPreviewExtensions.has(
        extension,
      )
    ) {
      this.preview.set({
        ...state,
        kind: 'text',
      });

      await this.renderTextPreview(
        blob,
      );

      return;
    }

    if (extension === 'eml') {
      this.preview.set({
        ...state,
        kind: 'email',
      });

      await this.renderEmailPreview(
        blob,
      );

      return;
    }

    if (extension === 'msg') {
      this.preview.set({
        ...state,
        kind: 'unsupported',
        note:
          'Outlook MSG files cannot be rendered reliably in the browser. Download the file to open it in Outlook or another compatible application.',
      });

      return;
    }

    if (
      extension === 'heic' ||
      extension === 'heif'
    ) {
      this.preview.set({
        ...state,
        kind: 'image',
      });

      await this.nextRender();

      await this.renderHeicPreview(
        blob,
        extension,
      );

      return;
    }

    if (
      extension === 'tif' ||
      extension === 'tiff'
    ) {
      this.preview.set({
        ...state,
        kind: 'image',
      });

      await this.nextRender();

      await this.renderTiffPreview(
        blob,
      );

      return;
    }

    if (extension === 'zip') {
      this.preview.set({
        ...state,
        kind: 'archive',
      });

      await this.nextRender();

      await this.renderArchivePreview(
        blob,
      );

      return;
    }

    if (
      extension === 'rar' ||
      extension === '7z'
    ) {
      this.preview.set({
        ...state,
        kind: 'unsupported',
        note:
          `${extension.toUpperCase()} archives can be uploaded and downloaded, but their contents are not previewed in the browser.`,
      });

      return;
    }

    if (
      this.browserImageExtensions.has(
        extension,
      ) ||
      contentType.startsWith(
        'image/',
      )
    ) {
      this.preview.set({
        ...state,
        kind: 'image',
      });

      this.attachObjectUrl(blob);

      return;
    }

    if (
      extension === 'pdf' ||
      contentType ===
        'application/pdf'
    ) {
      this.preview.set({
        ...state,
        kind: 'pdf',
      });

      this.attachObjectUrl(
        blob,
        true,
      );

      return;
    }

    if (
      this.videoExtensions.has(
        extension,
      ) ||
      contentType.startsWith(
        'video/',
      )
    ) {
      this.preview.set({
        ...state,
        kind: 'video',
      });

      this.attachObjectUrl(blob);

      return;
    }

    if (
      this.audioExtensions.has(
        extension,
      ) ||
      contentType.startsWith(
        'audio/',
      )
    ) {
      this.preview.set({
        ...state,
        kind: 'audio',
      });

      this.attachObjectUrl(blob);

      return;
    }

    this.preview.set({
      ...state,
      kind: 'unsupported',
      note:
        this.unsupportedPreviewNote(
          extension,
        ),
    });
  }

  private async renderExcelPreview(
    blob: Blob,
    isCsv: boolean,
  ): Promise<void> {
    try {
      let workbook: XLSX.WorkBook;

      if (isCsv) {
        const text =
          await blob.text();

        workbook = XLSX.read(
          text,
          {
            type: 'string',
          },
        );
      } else {
        const buffer =
          await blob.arrayBuffer();

        workbook = XLSX.read(
          new Uint8Array(buffer),
          {
            type: 'array',
          },
        );
      }

      const sheets =
        workbook.SheetNames.map(
          (sheetName) => {
            const worksheet =
              workbook.Sheets[
                sheetName
              ];

            const rows =
              XLSX.utils.sheet_to_json(
                worksheet,
                {
                  header: 1,
                  defval: '',
                  raw: false,
                },
              ) as unknown[][];

            return {
              name: sheetName,

              html:
                this.sanitizer.bypassSecurityTrustHtml(
                  this.buildExcelTable(
                    rows,
                  ),
                ),
            };
          },
        );

      this.excelSheets.set(
        sheets,
      );

      this.activeSheetIndex.set(0);
    } catch {
      this.excelSheets.set([]);

      throw new Error(
        'Unable to parse spreadsheet.',
      );
    }
  }

  private buildExcelTable(
    rows: unknown[][],
  ): string {
    if (!rows.length) {
      return `
        <div class="excel-empty">
          No spreadsheet data available.
        </div>
      `;
    }

    let html =
      '<table class="excel-table">';

    rows.forEach((row) => {
      html += '<tr>';

      const cells =
        Array.isArray(row)
          ? row
          : [];

      cells.forEach((cell) => {
        html += `<td>${this.escapeHtml(
          cell === null ||
            cell === undefined
            ? ''
            : String(cell),
        )}</td>`;
      });

      html += '</tr>';
    });

    html += '</table>';

    return html;
  }

  private async renderDocxPreview(
    blob: Blob,
  ): Promise<void> {
    const buffer =
      await blob.arrayBuffer();

    await this.nextRender();

    const container =
      this.docxContainer
        ?.nativeElement;

    if (!container) {
      throw new Error(
        'DOCX preview container is unavailable.',
      );
    }

    container.replaceChildren();

    try {
      await renderAsync(
        buffer,
        container,
      );
    } catch {
      throw new Error(
        'Unable to render Word document.',
      );
    }
  }

  private async renderHeicPreview(
    blob: Blob,
    extension: string,
  ): Promise<void> {
    try {
      const ext = this.normalizeExtension(extension);
      const buffer = await blob.arrayBuffer();

      // The API/storage layer may return application/octet-stream.
      // heic2any works more reliably when the Blob has the real image MIME type.
      const sourceBlob = new Blob(
        [buffer],
        {
          type:
            ext === 'heif'
              ? 'image/heif'
              : 'image/heic',
        },
      );

      const converted = await heic2any({
        blob: sourceBlob,
        toType: 'image/jpeg',
        quality: 0.92,
      });

      const convertedBlob = Array.isArray(converted)
        ? converted[0]
        : converted;

      if (!(convertedBlob instanceof Blob)) {
        throw new Error(
          'HEIC/HEIF conversion returned no image.',
        );
      }

      this.attachObjectUrl(convertedBlob);
    } catch (error) {
      console.error(
        'HEIC/HEIF preview conversion failed:',
        error,
      );

      const current = this.preview();

      if (current) {
        this.preview.set({
          ...current,
          kind: 'unsupported',
          note:
            'The HEIC/HEIF image could not be converted for browser preview. The original file can still be downloaded.',
        });
      }
    }
  }

  private async renderTiffPreview(
    blob: Blob,
  ): Promise<void> {
    try {
      const UTIF =
        (await import(
          'utif'
        )) as unknown as UtifModule;

      const buffer =
        await blob.arrayBuffer();

      const ifds =
        UTIF.decode(buffer);

      if (!ifds.length) {
        throw new Error(
          'TIFF contains no image pages.',
        );
      }

      const page = ifds[0];

      UTIF.decodeImage(
        buffer,
        page,
      );

      const rgba =
        UTIF.toRGBA8(page);

      const canvas =
        globalThis.document.createElement(
          'canvas',
        );

      canvas.width = page.width;
      canvas.height = page.height;

      const context =
        canvas.getContext('2d');

      if (!context) {
        throw new Error(
          'Canvas is unavailable.',
        );
      }

      const imageData =
        new ImageData(
          new Uint8ClampedArray(
            rgba,
          ),
          page.width,
          page.height,
        );

      context.putImageData(
        imageData,
        0,
        0,
      );

      const pngBlob =
        await new Promise<Blob>(
          (
            resolve,
            reject,
          ) => {
            canvas.toBlob(
              (value) => {
                if (value) {
                  resolve(value);
                } else {
                  reject(
                    new Error(
                      'Could not create TIFF preview.',
                    ),
                  );
                }
              },

              'image/png',
            );
          },
        );

      this.attachObjectUrl(
        pngBlob,
      );
    } catch {
      const current =
        this.preview();

      if (current) {
        this.preview.set({
          ...current,
          kind: 'unsupported',
          note:
            'The TIFF image could not be rendered. Multi-page TIFF files preview only their first page when supported.',
        });
      }
    }
  }

  private async renderTextPreview(
    blob: Blob,
  ): Promise<void> {
    const text =
      await blob.text();

    this.textLines.set(
      text
        .replace(/\r\n/g, '\n')
        .replace(/\r/g, '\n')
        .split('\n')
        .map((line, index) => ({
          n: index + 1,
          text: line,
        })),
    );
  }

  private async renderEmailPreview(
    blob: Blob,
  ): Promise<void> {
    const raw =
      await blob.text();

    const parsed =
      this.parseEml(raw);

    this.emailPreview.set(
      parsed,
    );

    if (parsed.isHtml) {
      this.buildEmailHtmlUrl(
        parsed.htmlBody,
      );
    }
  }

  private async renderArchivePreview(
    blob: Blob,
  ): Promise<void> {
    try {
      const zip =
        await JSZip.loadAsync(
          blob,
        );

      const entries =
        Object.values(
          zip.files,
        )
          .map((entry) => {
            const internal =
              entry as JSZipObject & {
                _data?: {
                  uncompressedSize?: number;
                };
              };

            return {
              name: entry.name,

              size:
                internal._data
                  ?.uncompressedSize ??
                0,

              isDir: entry.dir,
            };
          })
          .sort((a, b) =>
            a.name.localeCompare(
              b.name,
            ),
          );

      this.archiveEntries.set(
        entries,
      );
    } catch {
      this.archiveEntries.set([]);

      throw new Error(
        'Unable to read ZIP archive.',
      );
    }
  }

  private parseEml(
    raw: string,
  ): EmailPreview {
    const text = raw
      .replace(/\r\n/g, '\n')
      .replace(/\r/g, '\n');

    const blankIndex =
      text.indexOf('\n\n');

    const rawHeaders =
      blankIndex >= 0
        ? text.substring(
            0,
            blankIndex,
          )
        : text;

    const rawBody =
      blankIndex >= 0
        ? text.substring(
            blankIndex + 2,
          )
        : '';

    const headers =
      rawHeaders.replace(
        /\n[ \t]+/g,
        ' ',
      );

    const getHeader = (
      name: string,
    ): string => {
      const match =
        headers.match(
          new RegExp(
            `^${name}:\\s*(.+)$`,
            'im',
          ),
        );

      return match
        ? this.decodeMimeWords(
            match[1].trim(),
          )
        : '';
    };

    const contentType =
      getHeader(
        'Content-Type',
      ) || 'text/plain';

    const boundary =
      this.extractBoundary(
        contentType,
      );

    let htmlBody = '';
    let textBody = '';

    const attachments: string[] =
      [];

    if (boundary) {
      const parts =
        this.splitMimeParts(
          text,
          boundary,
        );

      for (const part of parts) {
        const splitIndex =
          part.indexOf('\n\n');

        if (splitIndex < 0) {
          continue;
        }

        const partHeaders =
          part
            .substring(
              0,
              splitIndex,
            )
            .replace(
              /\n[ \t]+/g,
              ' ',
            );

        const partBody =
          part.substring(
            splitIndex + 2,
          );

        const partContentType =
          this.getMimeHeader(
            partHeaders,
            'Content-Type',
          );

        const disposition =
          this.getMimeHeader(
            partHeaders,
            'Content-Disposition',
          );

        const encoding =
          this.getMimeHeader(
            partHeaders,
            'Content-Transfer-Encoding',
          ).toLowerCase();

        const nestedBoundary =
          this.extractBoundary(
            partContentType,
          );

        const fileMatch =
          partHeaders.match(
            /(?:filename|name)="?([^";\n]+)"?/i,
          );

        if (
          fileMatch &&
          /attachment/i.test(
            disposition,
          )
        ) {
          attachments.push(
            this.decodeMimeWords(
              fileMatch[1].trim(),
            ),
          );

          continue;
        }

        if (
          nestedBoundary &&
          /multipart/i.test(
            partContentType,
          )
        ) {
          const nestedParts =
            this.splitMimeParts(
              partBody,
              nestedBoundary,
            );

          for (
            const nestedPart of
            nestedParts
          ) {
            const nestedIndex =
              nestedPart.indexOf(
                '\n\n',
              );

            if (
              nestedIndex < 0
            ) {
              continue;
            }

            const nestedHeaders =
              nestedPart
                .substring(
                  0,
                  nestedIndex,
                )
                .replace(
                  /\n[ \t]+/g,
                  ' ',
                );

            const nestedBody =
              nestedPart.substring(
                nestedIndex + 2,
              );

            const nestedType =
              this.getMimeHeader(
                nestedHeaders,
                'Content-Type',
              );

            const nestedEncoding =
              this.getMimeHeader(
                nestedHeaders,
                'Content-Transfer-Encoding',
              ).toLowerCase();

            const nestedDisposition =
              this.getMimeHeader(
                nestedHeaders,
                'Content-Disposition',
              );

            const nestedFile =
              nestedHeaders.match(
                /(?:filename|name)="?([^";\n]+)"?/i,
              );

            if (
              nestedFile &&
              /attachment/i.test(
                nestedDisposition,
              )
            ) {
              attachments.push(
                this.decodeMimeWords(
                  nestedFile[1].trim(),
                ),
              );

              continue;
            }

            if (
              /text\/html/i.test(
                nestedType,
              ) &&
              !htmlBody
            ) {
              htmlBody =
                this.decodeMimeBody(
                  nestedBody,
                  nestedEncoding,
                );
            } else if (
              /text\/plain/i.test(
                nestedType,
              ) &&
              !textBody
            ) {
              textBody =
                this.decodeMimeBody(
                  nestedBody,
                  nestedEncoding,
                );
            }
          }

          continue;
        }

        if (
          /text\/html/i.test(
            partContentType,
          ) &&
          !htmlBody
        ) {
          htmlBody =
            this.decodeMimeBody(
              partBody,
              encoding,
            );
        } else if (
          /text\/plain/i.test(
            partContentType,
          ) &&
          !textBody
        ) {
          textBody =
            this.decodeMimeBody(
              partBody,
              encoding,
            );
        }
      }
    } else {
      const encoding =
        getHeader(
          'Content-Transfer-Encoding',
        ).toLowerCase();

      if (
        /text\/html/i.test(
          contentType,
        )
      ) {
        htmlBody =
          this.decodeMimeBody(
            rawBody,
            encoding,
          );
      } else {
        textBody =
          this.decodeMimeBody(
            rawBody,
            encoding,
          );
      }
    }

    return {
      subject:
        getHeader('Subject') ||
        '(No subject)',

      from:
        getHeader('From'),

      to:
        getHeader('To'),

      cc:
        getHeader('Cc'),

      date:
        getHeader('Date'),

      body:
        textBody.trim(),

      htmlBody:
        htmlBody.trim(),

      isHtml:
        !!htmlBody.trim(),

      attachments,
    };
  }

  private getMimeHeader(
    headers: string,
    name: string,
  ): string {
    const match =
      headers.match(
        new RegExp(
          `^${name}:\\s*([^\\n]+)`,
          'im',
        ),
      );

    return match
      ? this.decodeMimeWords(
          match[1].trim(),
        )
      : '';
  }

  private extractBoundary(
    contentType: string,
  ): string {
    const match =
      contentType.match(
        /boundary="?([^";\s]+)"?/i,
      );

    return match
      ? match[1]
      : '';
  }

  private splitMimeParts(
    text: string,
    boundary: string,
  ): string[] {
    return text
      .split(
        `--${boundary}`,
      )
      .slice(1)
      .map((part) =>
        part
          .replace(
            /^\n/,
            '',
          )
          .replace(
            /\n?--\s*$/,
            '',
          ),
      )
      .filter(
        (part) =>
          !!part.trim() &&
          part.trim() !== '--',
      );
  }

  private decodeMimeBody(
    body: string,
    encoding: string,
  ): string {
    if (!body) {
      return '';
    }

    if (
      encoding.includes(
        'base64',
      )
    ) {
      try {
        const clean =
          body.replace(
            /\s/g,
            '',
          );

        const bytes =
          Uint8Array.from(
            atob(clean),
            (character) =>
              character.charCodeAt(
                0,
              ),
          );

        return new TextDecoder(
          'utf-8',
        ).decode(bytes);
      } catch {
        return body;
      }
    }

    if (
      encoding.includes(
        'quoted-printable',
      )
    ) {
      try {
        return new TextDecoder(
          'utf-8',
        ).decode(
          this.quotedPrintableBytes(
            body,
          ),
        );
      } catch {
        return body;
      }
    }

    return body;
  }

  private decodeMimeWords(
    value: string,
  ): string {
    return value.replace(
      /=\?([^?]+)\?([bq])\?([^?]*)\?=/gi,

      (
        original,
        charset: string,
        encoding: string,
        payload: string,
      ) => {
        try {
          let bytes: Uint8Array;

          if (
            encoding.toLowerCase() ===
            'b'
          ) {
            const binary =
              atob(
                payload.replace(
                  /\s/g,
                  '',
                ),
              );

            bytes =
              Uint8Array.from(
                binary,
                (character) =>
                  character.charCodeAt(
                    0,
                  ),
              );
          } else {
            bytes =
              this.quotedPrintableBytes(
                payload.replace(
                  /_/g,
                  ' ',
                ),
              );
          }

          try {
            return new TextDecoder(
              charset,
            ).decode(bytes);
          } catch {
            return new TextDecoder(
              'utf-8',
            ).decode(bytes);
          }
        } catch {
          return original;
        }
      },
    );
  }

  private quotedPrintableBytes(
    value: string,
  ): Uint8Array {
    const normalized =
      value.replace(
        /=\r?\n/g,
        '',
      );

    const bytes: number[] = [];

    for (
      let index = 0;
      index < normalized.length;
      index++
    ) {
      if (
        normalized[index] ===
          '=' &&
        /^[0-9a-fA-F]{2}$/.test(
          normalized.substring(
            index + 1,
            index + 3,
          ),
        )
      ) {
        bytes.push(
          parseInt(
            normalized.substring(
              index + 1,
              index + 3,
            ),
            16,
          ),
        );

        index += 2;
      } else {
        bytes.push(
          normalized.charCodeAt(
            index,
          ) & 0xff,
        );
      }
    }

    return new Uint8Array(
      bytes,
    );
  }

  private buildEmailHtmlUrl(
    html: string,
  ): void {
    if (this.emailObjectUrl) {
      URL.revokeObjectURL(
        this.emailObjectUrl,
      );

      this.emailObjectUrl = '';
    }

    const cleaned = html
      .replace(
        /<script\b[^>]*>[\s\S]*?<\/script>/gi,
        '',
      )
      .replace(
        /\son\w+\s*=\s*"[^"]*"/gi,
        '',
      )
      .replace(
        /\son\w+\s*=\s*'[^']*'/gi,
        '',
      )
      .replace(
        /\son\w+\s*=\s*[^\s>]+/gi,
        '',
      )
      .replace(
        /javascript:/gi,
        'nojavascript:',
      );

    const content = `
      <!doctype html>
      <html>
        <head>
          <meta charset="utf-8" />
          <base target="_blank" />

          <style>
            html,
            body {
              margin: 0;
              padding: 16px;
              font-family: Arial, sans-serif;
              font-size: 14px;
              line-height: 1.55;
              color: #111827;
              background: #ffffff;
              overflow-x: auto;
            }

            img {
              max-width: 100%;
              height: auto;
            }

            table {
              max-width: 100%;
            }

            a {
              word-break: break-word;
            }
          </style>
        </head>

        <body>
          ${cleaned}
        </body>
      </html>
    `;

    const blob =
      new Blob(
        [content],
        {
          type: 'text/html',
        },
      );

    this.emailObjectUrl =
      URL.createObjectURL(blob);

    this.emailHtmlUrl.set(
      this.sanitizer.bypassSecurityTrustResourceUrl(
        this.emailObjectUrl,
      ),
    );
  }

  private attachObjectUrl(
    blob: Blob,
    resourceUrl = false,
  ): void {
    const current =
      this.preview();

    if (!current) {
      return;
    }

    if (current.rawUrl) {
      URL.revokeObjectURL(
        current.rawUrl,
      );
    }

    const rawUrl =
      URL.createObjectURL(blob);

    this.preview.set({
      ...current,

      rawUrl,

      resourceUrl:
        resourceUrl
          ? this.sanitizer.bypassSecurityTrustResourceUrl(
              rawUrl,
            )
          : undefined,
    });
  }

  private setUnsupportedPreview(
    document: StoredDocument,
    blob: Blob,
    note: string,
  ): void {
    this.previewBlob = blob;

    const extension =
      this.normalizeExtension(
        document.extension ||
          document.fileName,
      );

    this.preview.set({
      document,
      name: document.fileName,
      extension,
      contentType:
        blob.type ||
        document.contentType ||
        'application/octet-stream',
      kind: 'unsupported',
      note,
    });
  }

  private unsupportedPreviewNote(
    extension: string,
  ): string {
    switch (extension) {
      case 'ppt':
      case 'pptx':
      case 'odp':
        return 'PowerPoint presentations can be uploaded and downloaded, but they are not rendered directly in the browser.';

      case 'rtf':
      case 'odt':
        return 'This document format can be uploaded and downloaded, but browser preview is not available.';

      case 'msg':
        return 'Outlook MSG files can be uploaded and downloaded, but browser preview is not available.';

      case 'rar':
      case '7z':
        return `${extension.toUpperCase()} archives can be uploaded and downloaded, but archive preview is not available.`;

      default:
        return 'Preview is not available for this format. Use download to open the file in its default application.';
    }
  }

  private clearPreviewData(): void {
    const current =
      this.preview();

    if (current?.rawUrl) {
      URL.revokeObjectURL(
        current.rawUrl,
      );
    }

    if (this.emailObjectUrl) {
      URL.revokeObjectURL(
        this.emailObjectUrl,
      );

      this.emailObjectUrl = '';
    }

    this.docxContainer
      ?.nativeElement
      .replaceChildren();

    this.preview.set(null);
    this.previewBlob = null;

    this.excelSheets.set([]);
    this.activeSheetIndex.set(0);

    this.textLines.set([]);

    this.archiveEntries.set([]);

    this.emailPreview.set(null);
    this.emailHtmlUrl.set(null);
  }

  private normalizeExtension(
    value: string,
  ): string {
    const normalized =
      value
        .trim()
        .toLowerCase()
        .split('?')[0]
        .split('#')[0];

    if (!normalized) {
      return '';
    }

    const withoutDot =
      normalized.startsWith('.')
        ? normalized.substring(1)
        : normalized;

    if (
      !withoutDot.includes('.')
    ) {
      return withoutDot;
    }

    return (
      withoutDot
        .split('.')
        .pop() ?? ''
    );
  }

  private escapeHtml(
    value: string,
  ): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  private downloadBlob(
    blob: Blob,
    fileName: string,
  ): void {
    const url =
      URL.createObjectURL(blob);

    const anchor =
      globalThis.document.createElement(
        'a',
      );

    anchor.href = url;
    anchor.download = fileName;
    anchor.rel = 'noopener';

    globalThis.document.body.appendChild(
      anchor,
    );

    anchor.click();
    anchor.remove();

    setTimeout(() => {
      URL.revokeObjectURL(url);
    }, 0);
  }

  private nextRender(): Promise<void> {
    return new Promise(
      (resolve) => {
        setTimeout(
          resolve,
          0,
        );
      },
    );
  }


}

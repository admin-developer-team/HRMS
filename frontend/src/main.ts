import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { normalizeLegacyActivationUrl } from './app/core/activation-url';

normalizeLegacyActivationUrl(window.location, window.history);

bootstrapApplication(App, appConfig).catch((err) => console.error(err));

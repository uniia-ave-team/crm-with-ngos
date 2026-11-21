import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { registerIcons } from './app/core/icons';

registerIcons();

bootstrapApplication(AppComponent, appConfig).catch((error: unknown) => console.error(error));

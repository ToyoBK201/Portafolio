import { bootstrapApplication } from "@angular/platform-browser";
import { provideHttpClient, withInterceptors } from "@angular/common/http";
import { provideRouter } from "@angular/router";
import { AppComponent } from "./app/app.component";
import { routes } from "./app/app.routes";
import { authInterceptor } from "./app/core/auth.interceptor";
import { correlationIdInterceptor } from "./app/core/correlation-id.interceptor";

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptors([correlationIdInterceptor, authInterceptor])),
    provideRouter(routes)
  ]
}).catch((error) => console.error(error));

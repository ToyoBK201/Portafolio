import { ChangeDetectorRef, Component, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { AuthService, DevTokenRole } from "../../core/auth.service";
import { DEFAULT_REQUESTER_USER_ID } from "../../core/app.constants";

@Component({
  selector: "app-auth-dev",
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: "./auth-dev.component.html"
})
export class AuthDevComponent {
  readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);

  authError = "";
  authBusy = false;

  readonly devAuthForm = this.fb.group({
    userId: [DEFAULT_REQUESTER_USER_ID, [Validators.required]],
    role: ["Requester" as DevTokenRole, [Validators.required]]
  });

  async requestDevToken(): Promise<void> {
    if (this.devAuthForm.invalid) {
      this.devAuthForm.markAllAsTouched();
      return;
    }
    this.authBusy = true;
    this.authError = "";
    try {
      const raw = this.devAuthForm.getRawValue();
      await this.auth.requestDevToken(raw.userId ?? "", raw.role ?? "Requester");
    } catch {
      this.authError = "No se pudo obtener el token. Verifica que la API este en modo desarrollo (endpoint /auth/dev-token).";
    } finally {
      this.authBusy = false;
      this.cdr.detectChanges();
    }
  }

  logout(): void {
    this.auth.clearToken();
  }
}

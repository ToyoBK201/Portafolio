import { ChangeDetectorRef, Component, OnInit, inject } from "@angular/core";
import { HttpErrorResponse } from "@angular/common/http";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { firstValueFrom } from "rxjs";
import { parseApiProblem } from "../../core/api-problem";
import { AuthService } from "../../core/auth.service";
import { getHttpErrorMessage } from "../../core/http-error.util";

@Component({
  selector: "app-login",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: "./login.component.html"
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  busy = false;
  errorMessage = "";
  sessionHint = "";
  returnUrl = "/solicitudes/bandeja";

  readonly form = this.fb.group({
    email: ["", [Validators.required, Validators.email]],
    password: ["", [Validators.required, Validators.minLength(1)]]
  });

  ngOnInit(): void {
    const reason = this.route.snapshot.queryParamMap.get("reason");
    const requested = this.route.snapshot.queryParamMap.get("returnUrl");
    this.returnUrl = this.normalizeReturnUrl(requested);
    if (reason === "session") {
      this.sessionHint =
        "Su sesión expiró o el token dejó de ser válido. Vuelva a iniciar sesión.";
    }
  }

  private normalizeReturnUrl(value: string | null): string {
    if (!value) {
      return "/solicitudes/bandeja";
    }
    // Evita open redirect: solo rutas internas relativas al dominio actual.
    if (!value.startsWith("/")) {
      return "/solicitudes/bandeja";
    }
    if (value.startsWith("//")) {
      return "/solicitudes/bandeja";
    }
    return value;
  }

  private getLoginErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 429) {
      const p = parseApiProblem(error.error);
      const ref = p?.correlationId ? ` Ref: ${p.correlationId}.` : "";
      return `Demasiados intentos de inicio de sesión. Espere 1 minuto e intente nuevamente.${ref}`;
    }

    return getHttpErrorMessage(error, "No se pudo iniciar sesión.");
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy = true;
    this.errorMessage = "";
    try {
      const raw = this.form.getRawValue();
      await this.auth.loginWithPassword((raw.email ?? "").trim(), raw.password ?? "");
      await this.router.navigateByUrl(this.returnUrl);
    } catch (e) {
      this.errorMessage = this.getLoginErrorMessage(e);
    } finally {
      this.busy = false;
      this.cdr.detectChanges();
    }
  }
}

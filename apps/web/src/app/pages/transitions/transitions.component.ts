import { HttpErrorResponse } from "@angular/common/http";
import { ChangeDetectorRef, Component, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { firstValueFrom, timeout } from "rxjs";
import { ApiClientService } from "../../core/api-client.service";
import { getHttpErrorMessage } from "../../core/http-error.util";

@Component({
  selector: "app-transitions",
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: "./transitions.component.html"
})
export class TransitionsComponent {
  private readonly apiClient = inject(ApiClientService);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);

  transitionBusy = false;
  transitionError = "";
  transitionSuccess = "";

  readonly transitionForm = this.fb.group({
    requestId: [""],
    transition: ["Submit", [Validators.required]],
    reason: [""]
  });

  async applyTransition(): Promise<void> {
    if (this.transitionForm.invalid) {
      this.transitionForm.markAllAsTouched();
      return;
    }

    const raw = this.transitionForm.getRawValue();
    const requestId = (raw.requestId ?? "").trim();
    if (!requestId) {
      this.transitionError = "Debes indicar un RequestId.";
      return;
    }

    this.transitionBusy = true;
    this.transitionError = "";
    this.transitionSuccess = "";
    try {
      await firstValueFrom(
        this.apiClient
          .transitionRequest(requestId, {
            transition: raw.transition ?? "Submit",
            reason: raw.reason ? raw.reason : null
          })
          .pipe(timeout(60_000))
      );

      this.transitionSuccess = "Transición aplicada correctamente.";
    } catch (err: unknown) {
      this.transitionError = getHttpErrorMessage(
        err,
        "No se pudo aplicar la transición (valida estado, rol y motivo)."
      );
      if (err instanceof HttpErrorResponse && err.status === 409) {
        this.transitionError += " Recarga el detalle de la solicitud para ver el estado actual.";
      }
    } finally {
      this.transitionBusy = false;
      this.cdr.detectChanges();
    }
  }
}

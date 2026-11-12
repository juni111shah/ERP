import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, throwError } from 'rxjs';
import { errorBody } from './problem-details';

const MODULE_DISABLED = 'https://erp.local/problems/module-disabled'; // ModuleProblems.ModuleDisabledType (scaffold)

/** A module disabled while the user is on its screen: API returns 403 module-disabled → toast + go home. */
export const moduleDisabledInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const message = inject(NzMessageService);
  return next(req).pipe(
    catchError((err: unknown) => {
      const body = err instanceof HttpErrorResponse && err.status === 403 ? errorBody(err) : null;
      if (body?.type === MODULE_DISABLED) {
        message.warning(body.detail ?? 'This module is not enabled for your workspace.');
        void router.navigateByUrl('/');
      }
      return throwError(() => err);
    }),
  );
};

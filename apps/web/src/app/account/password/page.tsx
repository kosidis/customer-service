import { ChangePasswordForm } from "@/components/ChangePasswordForm";
import { requireUser } from "@/lib/auth/session";
import { initialChangePasswordFormState } from "@/lib/form-state";
import { changePasswordAction } from "../actions";

export const dynamic = "force-dynamic";

export default async function ChangePasswordPage() {
  const user = await requireUser({ allowPendingChange: true });
  return (
    <div>
      <h1 className="mb-4 text-2xl font-semibold">Change password</h1>
      {user.mustChangePassword ? (
        <p className="mb-4 max-w-sm text-sm text-slate-700">
          You must set a new password before continuing. It must differ from your current one.
        </p>
      ) : null}
      <ChangePasswordForm action={changePasswordAction} initialState={initialChangePasswordFormState} />
    </div>
  );
}

import { redirect } from "next/navigation";
import { LoginForm } from "@/components/LoginForm";
import { getCurrentUser } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/http";
import { getSessionToken } from "@/lib/auth/session";
import { initialLoginFormState } from "@/lib/form-state";
import { loginAction } from "./actions";

export const dynamic = "force-dynamic";

async function isSignedIn(): Promise<boolean> {
  if (!(await getSessionToken())) return false;
  try {
    await getCurrentUser();
    return true;
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return false;
    throw error;
  }
}

export default async function LoginPage() {
  if (await isSignedIn()) redirect("/customers");
  return (
    <div>
      <h1 className="mb-4 text-2xl font-semibold">Sign in</h1>
      <LoginForm action={loginAction} initialState={initialLoginFormState} />
    </div>
  );
}

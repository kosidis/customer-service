import { CustomerForm } from "@/components/CustomerForm";
import { requireUser } from "@/lib/auth/session";
import { initialFormState } from "@/lib/form-state";
import { createCustomerAction } from "../actions";

export default async function NewCustomerPage() {
  await requireUser({ role: "Admin" });
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Add customer</h1>
      <CustomerForm action={createCustomerAction} initialState={initialFormState} submitLabel="Create customer" />
    </div>
  );
}

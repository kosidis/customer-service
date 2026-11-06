import { CustomerForm } from "@/components/CustomerForm";
import { initialFormState } from "@/lib/form-state";
import { createCustomerAction } from "../actions";

export default function NewCustomerPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Add customer</h1>
      <CustomerForm action={createCustomerAction} initialState={initialFormState} submitLabel="Create customer" />
    </div>
  );
}

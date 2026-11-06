import { notFound } from "next/navigation";
import { CustomerForm } from "@/components/CustomerForm";
import { getCustomer } from "@/lib/api/customers";
import { updateCustomerAction } from "../../actions";

export const dynamic = "force-dynamic";

export default async function EditCustomerPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const customer = await getCustomer(id);
  if (!customer) {
    notFound();
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Edit customer</h1>
      <CustomerForm
        action={updateCustomerAction.bind(null, id)}
        initialState={{
          fieldErrors: {},
          formError: null,
          values: {
            firstName: customer.firstName,
            lastName: customer.lastName,
            email: customer.email,
            phoneNumber: customer.phoneNumber ?? "",
          },
        }}
        submitLabel="Save changes"
      />
    </div>
  );
}

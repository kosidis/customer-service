import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CustomerForm } from "@/components/CustomerForm";
import { CustomerTable } from "@/components/CustomerTable";
import { DeleteCustomerButton } from "@/components/DeleteCustomerButton";
import { Pagination } from "@/components/Pagination";
import { SearchBox } from "@/components/SearchBox";
import { deleteCustomerAction } from "@/app/customers/actions";
import { initialFormState, type CustomerFormState } from "@/lib/form-state";
import CustomersError from "@/app/customers/error";
import { makeCustomer } from "./fixtures";

jest.mock("@/app/customers/actions", () => ({
  deleteCustomerAction: jest.fn(),
}));

const deleteMock = deleteCustomerAction as jest.Mock;

describe("CustomerForm", () => {
  const filledState: CustomerFormState = {
    fieldErrors: {},
    formError: null,
    values: { firstName: "Ada", lastName: "Lovelace", email: "ada@x.io", phoneNumber: "123", domain: "example.com" },
  };

  it("pre-fills values and sets HTML constraints", () => {
    render(<CustomerForm action={jest.fn()} initialState={filledState} submitLabel="Save" />);
    expect(screen.getByLabelText(/first name/i)).toHaveValue("Ada");
    expect(screen.getByLabelText(/last name/i)).toHaveValue("Lovelace");
    const email = screen.getByLabelText(/email/i);
    expect(email).toHaveValue("ada@x.io");
    expect(email).toHaveAttribute("type", "email");
    expect(email).toBeRequired();
    expect(email).toHaveAttribute("maxlength", "256");
    const phone = screen.getByLabelText(/phone/i);
    expect(phone).toHaveAttribute("type", "tel");
    expect(phone).not.toBeRequired();
    expect(phone).toHaveAttribute("maxlength", "32");
    const domain = screen.getByLabelText(/domain/i);
    expect(domain).toHaveValue("example.com");
    expect(domain).not.toBeRequired();
    expect(domain).toHaveAttribute("maxlength", "253");
    expect(screen.getByRole("button", { name: "Save" })).toBeEnabled();
  });

  it("renders field errors and a form error with aria wiring", () => {
    render(
      <CustomerForm
        action={jest.fn()}
        submitLabel="Save"
        initialState={{
          ...filledState,
          fieldErrors: { email: ["Taken"], firstName: ["Required", "Too short"] },
          formError: "Oops",
        }}
      />,
    );
    const email = screen.getByLabelText(/email/i);
    expect(email).toHaveAttribute("aria-invalid", "true");
    expect(email).toHaveAccessibleDescription("Taken");
    expect(screen.getByLabelText(/first name/i)).toHaveAccessibleDescription("Required Too short");
    expect(screen.getByLabelText(/last name/i)).toHaveAttribute("aria-invalid", "false");
    expect(screen.getByRole("alert")).toHaveTextContent("Oops");
  });

  it("disables the submit button while pending", async () => {
    let resolve!: (s: CustomerFormState) => void;
    const action = jest.fn(
      () => new Promise<CustomerFormState>((r) => { resolve = r; }),
    );
    render(<CustomerForm action={action} initialState={initialFormState} submitLabel="Create" />);
    await userEvent.type(screen.getByLabelText(/first name/i), "A");
    await userEvent.type(screen.getByLabelText(/last name/i), "B");
    await userEvent.type(screen.getByLabelText(/email/i), "a@b.co");
    await userEvent.click(screen.getByRole("button", { name: "Create" }));
    const button = await screen.findByRole("button", { name: "Saving..." });
    expect(button).toBeDisabled();
    expect(action).toHaveBeenCalledTimes(1);
    resolve({ fieldErrors: { email: ["Taken"] }, formError: null, values: { firstName: "A", lastName: "B", email: "a@b.co", phoneNumber: "", domain: "" } });
    expect(await screen.findByText("Taken")).toBeInTheDocument();
    expect(screen.getByLabelText(/first name/i)).toHaveValue("A");
    await waitFor(() => expect(screen.getByRole("button", { name: "Create" })).toBeEnabled());
  });
});

describe("CustomerTable", () => {
  it("renders rows with edit links and delete buttons", () => {
    render(
      <CustomerTable
        customers={[
          makeCustomer(),
          makeCustomer({ id: "22222222-2222-2222-2222-222222222222", firstName: "Alan", lastName: "Turing", email: "alan@x.io", phoneNumber: null, domain: null }),
        ]}
      />,
    );
    expect(screen.getAllByRole("row")).toHaveLength(3);
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    expect(screen.getByText("ada@example.com")).toBeInTheDocument();
    expect(screen.getAllByText("-")).toHaveLength(2);
    expect(screen.getByText("example.com")).toBeInTheDocument();
    expect(screen.getAllByText(/3 Feb 2026/)).toHaveLength(2);
    expect(screen.getAllByText(/3 Feb 2026/)[0]).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Edit Ada Lovelace" })).toHaveAttribute(
      "href",
      "/customers/11111111-1111-1111-1111-111111111111/edit",
    );
    expect(screen.getByRole("button", { name: "Delete Alan Turing" })).toBeInTheDocument();
  });

  it("wraps a long unbreakable domain, clamps it to three lines and keeps the full value in a tooltip", () => {
    const longDomain = "a".repeat(253);
    render(<CustomerTable customers={[makeCustomer({ domain: longDomain })]} />);
    const text = screen.getByText(longDomain);
    expect(text).toHaveAttribute("title", longDomain);
    expect(text).toHaveClass("line-clamp-3", "break-words", "max-w-[14rem]");
    expect(text).not.toHaveClass("truncate");
  });

  it("wraps a long unbreakable email, clamps it to three lines and keeps the full value in a tooltip", () => {
    const longEmail = `${"a".repeat(240)}@example.com`;
    render(<CustomerTable customers={[makeCustomer({ email: longEmail })]} />);
    const text = screen.getByText(longEmail);
    expect(text).toHaveAttribute("title", longEmail);
    expect(text).toHaveClass("line-clamp-3", "break-words", "max-w-[14rem]");
    expect(text).not.toHaveClass("truncate");
  });

  it("pins the actions column to the right edge so Edit and Delete stay visible", () => {
    render(<CustomerTable customers={[makeCustomer()]} />);
    const actionsCell = screen.getByRole("link", { name: "Edit Ada Lovelace" }).closest("td");
    expect(actionsCell).toHaveClass("sticky", "right-0", "whitespace-nowrap");
    const actionsHeader = screen.getByRole("columnheader", { name: "Actions" });
    expect(actionsHeader).toHaveClass("sticky", "right-0");
  });

  it("renders an empty state", () => {
    render(<CustomerTable customers={[]} />);
    expect(screen.getByText("No customers found.")).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });
});

describe("DeleteCustomerButton", () => {
  beforeEach(() => deleteMock.mockReset());
  afterEach(() => jest.restoreAllMocks());

  it("does nothing when the confirmation is cancelled", async () => {
    const confirm = jest.spyOn(window, "confirm").mockReturnValue(false);
    render(<DeleteCustomerButton id="abc" name="Ada" />);
    await userEvent.click(screen.getByRole("button", { name: "Delete Ada" }));
    expect(confirm).toHaveBeenCalledWith(expect.stringContaining("Ada"));
    expect(deleteMock).not.toHaveBeenCalled();
  });

  it("calls the action when confirmed", async () => {
    jest.spyOn(window, "confirm").mockReturnValue(true);
    deleteMock.mockResolvedValue(undefined);
    render(<DeleteCustomerButton id="abc" name="Ada" />);
    await userEvent.click(screen.getByRole("button", { name: "Delete Ada" }));
    await waitFor(() => expect(deleteMock).toHaveBeenCalledWith("abc", undefined));
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("passes the return path to the action", async () => {
    jest.spyOn(window, "confirm").mockReturnValue(true);
    deleteMock.mockResolvedValue(undefined);
    render(<DeleteCustomerButton id="abc" name="Ada" returnTo="/customers?page=2" />);
    await userEvent.click(screen.getByRole("button", { name: "Delete Ada" }));
    await waitFor(() => expect(deleteMock).toHaveBeenCalledWith("abc", "/customers?page=2"));
  });

  it("shows an error when the action fails", async () => {
    jest.spyOn(window, "confirm").mockReturnValue(true);
    deleteMock.mockResolvedValue({ error: "Nope" });
    render(<DeleteCustomerButton id="abc" name="Ada" />);
    await userEvent.click(screen.getByRole("button", { name: "Delete Ada" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Nope");
  });
});

describe("Pagination", () => {
  it("renders nothing for a single page", () => {
    const { container } = render(<Pagination page={1} totalPages={1} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("keeps the search term in prev/next links", () => {
    render(<Pagination page={2} totalPages={3} search="ada & co" />);
    expect(screen.getByRole("link", { name: "Previous" })).toHaveAttribute("href", "/customers?search=ada+%26+co&page=1");
    expect(screen.getByRole("link", { name: "Next" })).toHaveAttribute("href", "/customers?search=ada+%26+co&page=3");
    expect(screen.getByText("Page 2 of 3")).toBeInTheDocument();
    expect(screen.getByText("Page 2 of 3")).not.toHaveAttribute("aria-current");
    expect(screen.getByRole("navigation", { name: "Customer list pagination" })).toBeInTheDocument();
  });

  it("disables previous on the first page and next on the last", () => {
    const { rerender } = render(<Pagination page={1} totalPages={2} />);
    expect(screen.queryByRole("link", { name: "Previous" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Next" })).toHaveAttribute("href", "/customers?page=2");
    rerender(<Pagination page={2} totalPages={2} />);
    expect(screen.queryByRole("link", { name: "Next" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Previous" })).toHaveAttribute("href", "/customers?page=1");
  });
});

describe("SearchBox", () => {
  it("is a GET form posting search to /customers with the current value", () => {
    render(<SearchBox defaultValue="ada" />);
    const form = screen.getByRole("search");
    expect(form).toHaveAttribute("method", "get");
    expect(form).toHaveAttribute("action", "/customers");
    expect(screen.getByRole("searchbox", { name: "Search customers" })).toHaveValue("ada");
    expect(screen.getByRole("searchbox")).toHaveAttribute("name", "search");
  });
});

describe("CustomersError", () => {
  it("shows a friendly message and retries", async () => {
    const reset = jest.fn();
    render(<CustomersError error={new Error("x")} reset={reset} />);
    expect(screen.getByText(/Couldn.t reach the customer service/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(reset).toHaveBeenCalled();
  });
});

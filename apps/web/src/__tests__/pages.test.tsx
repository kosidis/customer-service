import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { deleteCustomerAction } from "@/app/customers/actions";
import { notFound, redirect } from "next/navigation";
import Home from "@/app/page";
import CustomersPage from "@/app/customers/page";
import NewCustomerPage from "@/app/customers/new/page";
import EditCustomerPage from "@/app/customers/[id]/edit/page";
import { getCustomer, listCustomers } from "@/lib/api/customers";
import { makeCustomer } from "./fixtures";

jest.mock("next/navigation", () => ({
  redirect: jest.fn(),
  notFound: jest.fn(() => {
    throw new Error("NEXT_NOT_FOUND");
  }),
}));
jest.mock("@/lib/api/customers", () => ({
  listCustomers: jest.fn(),
  getCustomer: jest.fn(),
}));
jest.mock("@/app/customers/actions", () => ({
  createCustomerAction: jest.fn(),
  updateCustomerAction: jest.fn(),
  deleteCustomerAction: jest.fn(),
}));

const list = listCustomers as jest.Mock;

beforeEach(() => jest.clearAllMocks());

describe("pages", () => {
  it("root redirects to /customers", () => {
    Home();
    expect(redirect).toHaveBeenCalledWith("/customers");
  });

  it("lists customers using search and page params", async () => {
    list.mockResolvedValue({ items: [makeCustomer()], page: 2, pageSize: 20, totalCount: 30, totalPages: 3 });
    render(await CustomersPage({ searchParams: Promise.resolve({ search: " ada ", page: "2" }) }));
    expect(list).toHaveBeenCalledWith({ page: 2, search: "ada" });
    expect(screen.getByRole("heading", { name: "Customers" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Add customer" })).toHaveAttribute("href", "/customers/new");
    expect(screen.getByRole("searchbox")).toHaveValue("ada");
    expect(screen.getByRole("link", { name: "Next" })).toHaveAttribute("href", "/customers?search=ada&page=3");
    expect(redirect).not.toHaveBeenCalled();
  });

  it("redirects to the last page when the requested page is past the end", async () => {
    list.mockResolvedValue({ items: [], page: 5, pageSize: 20, totalCount: 30, totalPages: 2 });
    await CustomersPage({ searchParams: Promise.resolve({ search: "ada", page: "5" }) });
    expect(redirect).toHaveBeenCalledWith("/customers?search=ada&page=2");
  });

  it("passes the current list view to delete buttons", async () => {
    list.mockResolvedValue({ items: [makeCustomer()], page: 2, pageSize: 20, totalCount: 30, totalPages: 3 });
    render(await CustomersPage({ searchParams: Promise.resolve({ search: "ada", page: "2" }) }));
    jest.spyOn(window, "confirm").mockReturnValue(true);
    await userEvent.click(screen.getByRole("button", { name: /Delete/ }));
    expect(deleteCustomerAction).toHaveBeenCalledWith(
      expect.any(String),
      "/customers?search=ada&page=2",
    );
    jest.restoreAllMocks();
  });

  it("falls back to page 1 for bad or array params", async () => {
    list.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
    render(await CustomersPage({ searchParams: Promise.resolve({ page: "abc", search: ["x", "y"] }) }));
    expect(list).toHaveBeenCalledWith({ page: 1, search: "x" });
    expect(screen.getByText("No customers found.")).toBeInTheDocument();

    await CustomersPage({ searchParams: Promise.resolve({ page: "0" }) });
    expect(list).toHaveBeenLastCalledWith({ page: 1, search: undefined });
  });

  it("renders the new customer form", () => {
    render(<NewCustomerPage />);
    expect(screen.getByRole("heading", { name: "Add customer" })).toBeInTheDocument();
    expect(screen.getByLabelText(/first name/i)).toHaveValue("");
  });

  it("pre-fills the edit form", async () => {
    (getCustomer as jest.Mock).mockResolvedValue(makeCustomer({ phoneNumber: null }));
    render(await EditCustomerPage({ params: Promise.resolve({ id: "abc" }) }));
    expect(getCustomer).toHaveBeenCalledWith("abc");
    expect(screen.getByLabelText(/email/i)).toHaveValue("ada@example.com");
    expect(screen.getByLabelText(/phone/i)).toHaveValue("");
  });

  it("calls notFound when the customer does not exist", async () => {
    (getCustomer as jest.Mock).mockResolvedValue(null);
    await expect(EditCustomerPage({ params: Promise.resolve({ id: "nope" }) })).rejects.toThrow("NEXT_NOT_FOUND");
    expect(notFound).toHaveBeenCalled();
  });
});

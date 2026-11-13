export interface CustomerFormValues {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  domain: string;
}

export interface CustomerFormState {
  fieldErrors: Record<string, string[]>;
  formError: string | null;
  values: CustomerFormValues;
}

export const emptyValues: CustomerFormValues = {
  firstName: "",
  lastName: "",
  email: "",
  phoneNumber: "",
  domain: "",
};

export const initialFormState: CustomerFormState = {
  fieldErrors: {},
  formError: null,
  values: emptyValues,
};

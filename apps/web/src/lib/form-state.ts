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

export interface LoginFormState {
  fieldErrors: Record<string, string[]>;
  formError: string | null;
  values: { username: string };
}

export const initialLoginFormState: LoginFormState = {
  fieldErrors: {},
  formError: null,
  values: { username: "" },
};

export interface ChangePasswordFormState {
  fieldErrors: Record<string, string[]>;
  formError: string | null;
}

export const initialChangePasswordFormState: ChangePasswordFormState = {
  fieldErrors: {},
  formError: null,
};

export interface IUser {
  email: string;
  token: string;
  displayName: string;
}

export interface ILoginValues {
  email: string;
  password: string;
}

export interface IRegisterValues extends ILoginValues {
  displayName: string;
}
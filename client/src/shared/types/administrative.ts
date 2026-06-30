export interface Province {
  code: string;
  name: string;
  type?: string;
}

export interface Ward {
  code: string;
  provinceCode?: string;
  name: string;
  type: string;
}

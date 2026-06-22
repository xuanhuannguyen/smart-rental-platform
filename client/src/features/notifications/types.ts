export interface Notification {
  id: string;
  type: string;
  title: string;
  body: string;
  referenceId?: string | null;
  referenceType?: string | null;
  isRead: boolean;
  createdAt: string;
}

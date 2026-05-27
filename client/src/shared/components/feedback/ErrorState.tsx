interface ErrorStateProps {
  message?: string;
}

export function ErrorState({ message = 'Đã xảy ra lỗi. Vui lòng thử lại.' }: ErrorStateProps) {
  return <div className="feedback-state feedback-error">{message}</div>;
}

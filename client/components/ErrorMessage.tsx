interface ErrorMessageProps {
  message: string;
}

export default function ErrorMessage({ message }: ErrorMessageProps) {
  return (
    <div className="error-message" role="alert">
      <span aria-hidden="true">⚠</span> {message}
    </div>
  );
}

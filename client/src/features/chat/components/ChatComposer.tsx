import React from 'react';

const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB
const ALLOWED_FILE_EXTS = ['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.txt'];

interface ChatComposerProps {
  value: string;
  onChange: (val: string) => void;
  imageUploading: boolean;
  fileUploading: boolean;
  onSendText: () => void;
  onSendIcon: (icon: string) => void;
  onImageSelected: (file: File | null) => void;
  onFileSelected: (file: File | null) => void;
  onError?: (msg: string) => void;
  isClosed: boolean;
  hasLeft: boolean;
  /** If true, Enter sends and Shift+Enter inserts a newline (ChatPage mode). Default: false (Enter always sends). */
  enterToSend?: boolean;
}

const quickEmojis = ['👍', '❤️', '😊', '🙏', '👌', '🔥', '🎉', '✅'];

export function ChatComposer({
  value,
  onChange,
  imageUploading,
  fileUploading,
  onSendText,
  onSendIcon,
  onImageSelected,
  onFileSelected,
  onError,
  isClosed,
  hasLeft,
  enterToSend = false
}: ChatComposerProps) {
  const isUploading = imageUploading || fileUploading;

  if (isClosed || hasLeft) {
    return (
      <footer className="chat-composer">
        <div className="chat-composer__disabled" style={{ textAlign: 'center', padding: '12px', color: '#64748b' }}>
          Cuộc trò chuyện đã đóng hoặc bạn đã rời nhóm.
        </div>
      </footer>
    );
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      if (enterToSend && e.shiftKey) return; // Shift+Enter = newline (not applicable to <input> but kept for textarea parity)
      e.preventDefault();
      onSendText();
    }
  };

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    event.currentTarget.value = '';
    if (!file) return;

    if (file.size > MAX_FILE_SIZE) {
      onError?.('Tệp tối đa 10MB.');
      return;
    }

    const ext = '.' + file.name.split('.').pop()?.toLowerCase();
    if (!ALLOWED_FILE_EXTS.includes(ext)) {
      onError?.('Chỉ hỗ trợ pdf, doc, docx, xls, xlsx, txt.');
      return;
    }

    onFileSelected(file);
  };

  return (
    <footer className="chat-composer">
      <div className="chat-emoji-row">
        {quickEmojis.map(icon => (
          <button key={icon} type="button" onClick={() => onSendIcon(icon)} disabled={isUploading}>
            {icon}
          </button>
        ))}
      </div>
      <div className="chat-composer__bar">
        {/* Image Attachment Button */}
        <label className="chat-image-button" title="Gửi ảnh" style={{ opacity: isUploading ? 0.5 : 1, pointerEvents: isUploading ? 'none' : 'auto' }}>
          <input
            type="file"
            accept=".jpg,.jpeg,.png,.webp,.gif,image/*"
            disabled={isUploading}
            onChange={event => {
              const file = event.target.files?.[0] ?? null;
              event.currentTarget.value = '';
              onImageSelected(file);
            }}
          />
          {imageUploading ? (
            <span className="chat-upload-spinner" />
          ) : (
            <svg style={{ width: '20px', height: '20px' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
              <circle cx="8.5" cy="8.5" r="1.5" />
              <polyline points="21 15 16 10 5 21" />
            </svg>
          )}
        </label>

        {/* File/Document Attachment Button */}
        <label className="chat-file-button" title="Gửi tệp" style={{ opacity: isUploading ? 0.5 : 1, pointerEvents: isUploading ? 'none' : 'auto' }}>
          <input
            type="file"
            accept=".pdf,.doc,.docx,.xls,.xlsx,.txt"
            disabled={isUploading}
            onChange={handleFileChange}
          />
          {fileUploading ? (
            <span className="chat-upload-spinner" />
          ) : (
            <svg style={{ width: '20px', height: '20px' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
              <line x1="16" y1="13" x2="8" y2="13" />
              <line x1="16" y1="17" x2="8" y2="17" />
              <polyline points="10 9 9 9 8 9" />
            </svg>
          )}
        </label>

        {/* Input Bar Pill */}
        <div className="chat-composer__input-container">
          <input
            value={value}
            onChange={event => onChange(event.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Nhập tin nhắn..."
            disabled={isUploading}
          />
        </div>

        {/* Send Paper-plane Button */}
        <button
          type="button"
          onClick={onSendText}
          disabled={!value.trim() || isUploading}
          className="chat-composer__send-btn"
          title="Gửi tin nhắn"
        >
          <svg style={{ width: '18px', height: '18px', marginLeft: '2px' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <line x1="22" y1="2" x2="11" y2="13" />
            <polygon points="22 2 15 22 11 13 2 9 22 2" />
          </svg>
        </button>
      </div>
    </footer>
  );
}

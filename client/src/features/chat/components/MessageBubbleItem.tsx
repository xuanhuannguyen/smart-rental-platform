import React, { useEffect, useRef, useState } from 'react';
import type { ChatMessage } from '../types';
import { downloadChatFile } from '../api';
import { usePrivateChatMediaObjectUrl } from './PrivateChatImage';
import './MessageBubbleItem.css';

interface MessageBubbleItemProps {
  message: ChatMessage;
  mine: boolean;
  onDelete: (messageId: string) => void;
}

export function MessageBubbleItem({ message, mine, onDelete }: MessageBubbleItemProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const imageMediaAssetId = message.messageType === 'Image' ? message.mediaAssetId : null;
  const { objectUrl: imageObjectUrl, error: imageLoadError } = usePrivateChatMediaObjectUrl(imageMediaAssetId);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setMenuOpen(false);
      }
    }
    if (menuOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [menuOpen]);

  const isDeleted = Boolean(message.deletedAt);

  const formatFileSize = (bytes?: number | null) => {
    if (!bytes) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  };

  const handleDownloadFile = async () => {
    if (!message.mediaAssetId) return;

    try {
      const blob = await downloadChatFile(message.mediaAssetId);
      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = message.fileName ?? 'file';
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
    } catch {
      window.alert('Không thể tải tệp. Vui lòng thử lại.');
    }
  };

  const handleViewImage = () => {
    if (imageObjectUrl) {
      window.open(imageObjectUrl, '_blank', 'noopener,noreferrer');
    }
  };

  return (
    <div className={`message-row ${mine ? 'mine' : ''} ${isDeleted ? 'deleted' : ''}`}>
      <div className="message-bubble-wrapper">
        <div className={`message-bubble message-bubble--${message.messageType.toLowerCase()}`}>
          {!mine && <span className="message-sender">{message.senderName}</span>}
          
          {isDeleted ? (
            <span className="message-text-deleted">Tin nhắn đã bị xóa</span>
          ) : message.messageType === 'Image' && message.mediaAssetId ? (
            <div className="message-image-container" onClick={handleViewImage}>
              {imageLoadError ? (
                <span className="chat-media-error">Không tải được ảnh.</span>
              ) : imageObjectUrl ? (
                <img src={imageObjectUrl} alt="Ảnh chat" className="chat-bubble-image" />
              ) : (
                <span className="chat-media-loading">Đang tải ảnh...</span>
              )}
              <div className="image-overlay">
                <span>Xem ảnh</span>
              </div>
            </div>
          ) : message.messageType === 'File' && message.mediaAssetId ? (
            <div className="message-file-card" onClick={() => void handleDownloadFile()}>
              <div className="file-icon">
                <svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                  <polyline points="14 2 14 8 20 8" />
                  <line x1="16" y1="13" x2="8" y2="13" />
                  <line x1="16" y1="17" x2="8" y2="17" />
                  <polyline points="10 9 9 9 8 9" />
                </svg>
              </div>
              <div className="file-details">
                <span className="file-name" title={message.fileName ?? 'Tệp tin'}>
                  {message.fileName ?? 'Tệp tin đính kèm'}
                </span>
                <span className="file-size">{formatFileSize(message.fileSize)}</span>
              </div>
            </div>
          ) : (
            <span className="message-text-content">{message.content}</span>
          )}

          <div className="message-bubble-meta">
            <small>{formatTime(message.createdAt)}</small>
            {message.status === 'sending' && <small className="status-sending">• Đang gửi</small>}
            {message.status === 'error' && <small className="status-error">• Lỗi</small>}
          </div>
        </div>

        {!isDeleted && message.status !== 'sending' && message.status !== 'error' && (
          <div className="message-actions-container" ref={menuRef}>
            <button
              type="button"
              className={`message-menu-trigger ${menuOpen ? 'active' : ''}`}
              onClick={() => setMenuOpen(!menuOpen)}
              title="Thao tác"
            >
              <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                <circle cx="12" cy="5" r="2" />
                <circle cx="12" cy="12" r="2" />
                <circle cx="12" cy="19" r="2" />
              </svg>
            </button>
            
            {menuOpen && (
              <div className="message-actions-dropdown">
                {message.messageType === 'Image' && message.mediaAssetId && (
                  <button type="button" className="dropdown-item" onClick={() => { setMenuOpen(false); handleViewImage(); }}>
                    Xem ảnh
                  </button>
                )}
                {message.messageType === 'File' && message.mediaAssetId && (
                  <button type="button" className="dropdown-item" onClick={() => { setMenuOpen(false); void handleDownloadFile(); }}>
                    Mở/Tải file
                  </button>
                )}
                {mine && (
                  <button
                    type="button"
                    className="dropdown-item dropdown-item--danger"
                    onClick={() => {
                      setMenuOpen(false);
                      if (window.confirm('Bạn có chắc chắn muốn xóa tin nhắn này?')) {
                        onDelete(message.id);
                      }
                    }}
                  >
                    Xóa tin nhắn
                  </button>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function formatTime(value?: string | null) {
  if (!value) return '';
  const date = new Date(value);
  const hours = date.getHours().toString().padStart(2, '0');
  const minutes = date.getMinutes().toString().padStart(2, '0');
  return `${hours}:${minutes}`;
}

import { useState } from 'react';
import { toAssetUrl } from '../../../shared/api/assets';
import { tokenStorage } from '../../../shared/api/tokenStorage';
import type { ChatMessage } from '../types';

export interface ChatDetailsPanelProps {
  messages: ChatMessage[];
  onClearHistory?: () => void;
}

export function ChatDetailsPanel({ messages, onClearHistory }: ChatDetailsPanelProps) {
  const [mediaExpanded, setMediaExpanded] = useState(true);
  const [showMediaList, setShowMediaList] = useState(true);
  const [showFileList, setShowFileList] = useState(false);

  const mediaMessages = messages.filter((m) => m.messageType === 'Image' && m.imageUrl && !m.deletedAt);
  const fileMessages = messages.filter((m) => m.messageType === 'File' && m.fileUrl && !m.deletedAt);

  return (
    <div className="chat-details-panel" style={{
      width: '300px',
      borderLeft: '1px solid #dce6f3',
      background: '#ffffff',
      display: 'flex',
      flexDirection: 'column',
      height: '100%',
      minWidth: '300px'
    }}>
      <div style={{ padding: '16px', borderBottom: '1px solid #eef3fa', fontWeight: 'bold', fontSize: '16px' }}>
        Chi tiết cuộc trò chuyện
      </div>
      
      <div style={{ flex: 1, overflowY: 'auto', padding: '16px' }}>
        <div 
          onClick={() => setMediaExpanded(!mediaExpanded)}
          style={{ 
            display: 'flex', 
            justifyContent: 'space-between', 
            alignItems: 'center', 
            fontWeight: '600', 
            fontSize: '14px', 
            cursor: 'pointer',
            padding: '8px 0',
            color: '#1e293b'
          }}
        >
          <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
            Media & files
          </span>
          <span>{mediaExpanded ? '▲' : '▼'}</span>
        </div>

        {mediaExpanded && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '8px', paddingLeft: '8px' }}>
            <div 
              onClick={() => { setShowMediaList(!showMediaList); setShowFileList(false); }}
              style={{ display: 'flex', alignItems: 'center', gap: '10px', padding: '8px', cursor: 'pointer', borderRadius: '6px', background: showMediaList ? '#eef5ff' : 'transparent' }}
              className="media-toggle-row"
            >
              <span style={{ fontSize: '18px' }}>🖼️</span>
              <span style={{ fontSize: '13px', fontWeight: '500' }}>Media ({mediaMessages.length})</span>
            </div>

            <div 
              onClick={() => { setShowFileList(!showFileList); setShowMediaList(false); }}
              style={{ display: 'flex', alignItems: 'center', gap: '10px', padding: '8px', cursor: 'pointer', borderRadius: '6px', background: showFileList ? '#eef5ff' : 'transparent' }}
              className="files-toggle-row"
            >
              <span style={{ fontSize: '18px' }}>📄</span>
              <span style={{ fontSize: '13px', fontWeight: '500' }}>Files ({fileMessages.length})</span>
            </div>
          </div>
        )}

        {showMediaList && (
          <div style={{ marginTop: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 'bold', color: '#64748b', marginBottom: '8px' }}>ẢNH ĐÃ CHIA SẺ</div>
            {mediaMessages.length === 0 ? (
              <div style={{ fontSize: '13px', color: '#94a3b8', textAlign: 'center', padding: '16px 0' }}>Không có ảnh nào</div>
            ) : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '6px' }}>
                {mediaMessages.map(msg => (
                  <a key={msg.id} href={toAssetUrl(msg.imageUrl || '')} target="_blank" rel="noopener noreferrer">
                    <img 
                      src={toAssetUrl(msg.imageUrl || '')} 
                      alt="media" 
                      style={{ width: '100%', height: '70px', objectFit: 'cover', borderRadius: '4px', border: '1px solid #e2e8f0' }} 
                    />
                  </a>
                ))}
              </div>
            )}
          </div>
        )}

        {showFileList && (
          <div style={{ marginTop: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 'bold', color: '#64748b', marginBottom: '8px' }}>TÀI LIỆU</div>
            {fileMessages.length === 0 ? (
              <div style={{ fontSize: '13px', color: '#94a3b8', textAlign: 'center', padding: '16px 0' }}>Không có tài liệu nào</div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                {fileMessages.map(msg => (
                  <div 
                    key={msg.id} 
                    style={{ 
                      display: 'flex', 
                      alignItems: 'center', 
                      gap: '8px', 
                      padding: '8px', 
                      borderRadius: '6px', 
                      border: '1px solid #e2e8f0', 
                      backgroundColor: '#f8fafc',
                      fontSize: '12px'
                    }}
                  >
                    <span style={{ fontSize: '16px' }}>📄</span>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontWeight: '500', color: '#334155', textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap' }} title={msg.fileName || 'Tệp'}>
                        {msg.fileName || 'Tệp'}
                      </div>
                      <div style={{ fontSize: '10px', color: '#64748b' }}>
                        {msg.fileType?.split('/').pop()?.toUpperCase() || 'FILE'}
                      </div>
                    </div>
                    <button 
                      type="button" 
                      onClick={async () => {
                        try {
                          const token = tokenStorage.getAccessToken();
                          const url = `/api/chat/conversations/${msg.conversationId}/messages/${msg.id}/file`;
                          const res = await fetch(url, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
                          if (!res.ok) throw new Error();
                          const blob = await res.blob();
                          const link = document.createElement('a');
                          link.href = URL.createObjectURL(blob);
                          link.download = msg.fileName || 'file';
                          document.body.appendChild(link);
                          link.click();
                          link.remove();
                        } catch {
                          alert('Không thể tải tệp.');
                        }
                      }}
                      style={{ 
                        padding: '2px 6px', 
                        backgroundColor: '#e2e8f0', 
                        border: 'none', 
                        borderRadius: '4px', 
                        cursor: 'pointer',
                        fontSize: '11px',
                        fontWeight: '600'
                      }}
                    >
                      Tải
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      <div style={{ padding: '16px', borderTop: '1px solid #eef3fa' }}>
        <button 
          type="button" 
          onClick={onClearHistory}
          style={{ width: '100%', background: '#fee2e2', color: '#ef4444', border: 'none', padding: '10px', borderRadius: '8px', fontWeight: '600', cursor: 'pointer' }}
        >
          Xóa cuộc trò chuyện
        </button>
      </div>
    </div>
  );
}

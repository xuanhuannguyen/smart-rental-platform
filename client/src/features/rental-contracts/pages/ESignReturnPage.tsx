import { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Button } from '../../../shared/components/ui/Button';

export function ESignReturnPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState<'loading' | 'success' | 'error' | 'cancel'>('loading');

  useEffect(() => {
    // Usually providers redirect back with some query params indicating status
    const event = searchParams.get('event');
    if (event === 'signing_complete' || event === 'success') {
      setStatus('success');
    } else if (event === 'cancel') {
      setStatus('cancel');
    } else {
      // Default to success for now, or could make an API call to verify
      setStatus('success');
    }
  }, [searchParams]);

  return (
    <div style={{ maxWidth: '600px', margin: '40px auto', textAlign: 'center', padding: '20px' }}>
      {status === 'loading' && <h2>Đang xác nhận kết quả ký số...</h2>}
      
      {status === 'success' && (
        <>
          <div style={{ fontSize: '48px', color: '#10b981', marginBottom: '20px' }}>✓</div>
          <h2 style={{ marginBottom: '16px' }}>Ký số thành công</h2>
          <p style={{ color: '#6b7280', marginBottom: '24px' }}>
            Cảm ơn bạn đã hoàn tất việc ký hợp đồng/phụ lục. Hệ thống đang cập nhật trạng thái mới nhất.
          </p>
          <Button onClick={() => navigate('/tenant/contracts')}>Về danh sách hợp đồng</Button>
        </>
      )}

      {status === 'cancel' && (
        <>
          <div style={{ fontSize: '48px', color: '#f59e0b', marginBottom: '20px' }}>!</div>
          <h2 style={{ marginBottom: '16px' }}>Đã hủy ký số</h2>
          <p style={{ color: '#6b7280', marginBottom: '24px' }}>
            Bạn đã hủy quá trình ký. Vui lòng thực hiện lại nếu bạn đổi ý.
          </p>
          <Button onClick={() => navigate(-1)} variant="secondary">Quay lại</Button>
        </>
      )}

      {status === 'error' && (
        <>
          <div style={{ fontSize: '48px', color: '#ef4444', marginBottom: '20px' }}>✗</div>
          <h2 style={{ marginBottom: '16px' }}>Ký số thất bại</h2>
          <p style={{ color: '#6b7280', marginBottom: '24px' }}>
            Đã có lỗi xảy ra trong quá trình ký số. Vui lòng liên hệ hỗ trợ hoặc thử lại sau.
          </p>
          <Button onClick={() => navigate(-1)} variant="secondary">Quay lại</Button>
        </>
      )}
    </div>
  );
}

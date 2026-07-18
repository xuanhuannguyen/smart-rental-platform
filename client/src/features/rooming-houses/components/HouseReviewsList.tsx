import React, { useEffect, useState } from 'react';
import { getRoomingHouseReviews, replyRoomingHouseReview, reportRoomingHouseReview, deleteRoomingHouseReviewReply, checkRoomingHouseReviewEligibility, deleteRoomingHouseReview } from '../api';
import type { RoomingHouseReviewResponse, RoomingHouseReviewListResponse, RoomingHouseReviewEligibilitySummaryResponse } from '../types';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { Button } from '../../../shared/components/ui/Button';
import { Toast } from '../../../shared/components/ui/Toast';
import { useAuth } from '../../../app/providers/AuthProvider';
import { InlineReviewForm } from './InlineReviewForm';
import { ConfirmModal } from '../../../shared/components/ui/ConfirmModal';
import './HouseReviewsList.css';

interface HouseReviewsListProps {
  roomingHouseId: string;
  landlordUserId: string;
  roomingHouseName?: string;
  roomingHouseAvatarUrl?: string;
}

export const HouseReviewsList: React.FC<HouseReviewsListProps> = ({ roomingHouseId, landlordUserId, roomingHouseName, roomingHouseAvatarUrl }) => {
  const { currentUser } = useAuth();
  const isLandlord = currentUser?.userId === landlordUserId;
  
  const [reviewsData, setReviewsData] = useState<RoomingHouseReviewListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  
  // Pagination state
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);

  // Reply state
  const [replyingToId, setReplyingToId] = useState<string | null>(null);
  const [replyContent, setReplyContent] = useState('');
  const [isSubmittingReply, setIsSubmittingReply] = useState(false);
  
  // Report state
  const [reportingId, setReportingId] = useState<string | null>(null);
  const [reportReason, setReportReason] = useState('');
  const [isSubmittingReport, setIsSubmittingReport] = useState(false);

  // Review state
  const [reviewEligibility, setReviewEligibility] = useState<RoomingHouseReviewEligibilitySummaryResponse | null>(null);
  const [editingReviewId, setEditingReviewId] = useState<string | null>(null);
  const [activeReviewContractId, setActiveReviewContractId] = useState<string | null>(null);
  const [isCheckingEligibility, setIsCheckingEligibility] = useState(false);

  // Image gallery state
  const [activeImageGallery, setActiveImageGallery] = useState<{ images: any[]; index: number } | null>(null);

  // Toast state
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  // Delete confirm state
  const [deleteModalState, setDeleteModalState] = useState<{
    isOpen: boolean;
    type: 'review' | 'reply' | null;
    targetId: string | null;
  }>({ isOpen: false, type: null, targetId: null });

  useEffect(() => {
    setPage(1);
    setActiveReviewContractId(null);
    setEditingReviewId(null);
    loadReviews(1, false);
    if (currentUser && !isLandlord) {
      checkEligibility();
    }
  }, [roomingHouseId, currentUser, isLandlord]);

  const checkEligibility = async () => {
    try {
      setIsCheckingEligibility(true);
      const eligibility = await checkRoomingHouseReviewEligibility(roomingHouseId);
      setReviewEligibility(eligibility);
    } catch (err) {
      console.error('Failed to check review eligibility', err);
    } finally {
      setIsCheckingEligibility(false);
    }
  };

  const loadReviews = async (pageIndex: number = 1, append: boolean = false) => {
    if (append) {
      setIsLoadingMore(true);
    } else {
      setLoading(true);
    }
    try {
      const data = await getRoomingHouseReviews(roomingHouseId, pageIndex, 5);
      
      setReviewsData(prev => {
        if (append && prev) {
          return {
            ...data,
            reviews: [...prev.reviews, ...data.reviews]
          };
        }
        return data;
      });
      
      setHasMore(data.reviews.length === 5 && (append ? (reviewsData?.reviews.length || 0) : 0) + data.reviews.length < data.totalReviews);
    } catch (err) {
      setError('Không thể tải danh sách đánh giá.');
    } finally {
      setLoading(false);
      setIsLoadingMore(false);
    }
  };

  const refreshCurrentList = async () => {
    try {
      // Reload up to current page to keep context
      const data = await getRoomingHouseReviews(roomingHouseId, 1, page * 5);
      setReviewsData(data);
      setHasMore(data.reviews.length < data.totalReviews);
    } catch (err) {
      console.error(err);
    }
  };

  const submitReply = async (reviewId: string) => {
    if (!replyContent.trim()) return;
    try {
      setIsSubmittingReply(true);
      await replyRoomingHouseReview(reviewId, { reply: replyContent });
      await refreshCurrentList();
      setReplyingToId(null);
      setReplyContent('');
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Lỗi khi gửi phản hồi.'), type: 'error' });
    } finally {
      setIsSubmittingReply(false);
    }
  };
  const handleDeleteReplyClick = (reviewId: string) => {
    setDeleteModalState({ isOpen: true, type: 'reply', targetId: reviewId });
  };

  const handleDeleteReviewClick = (reviewId: string) => {
    setDeleteModalState({ isOpen: true, type: 'review', targetId: reviewId });
  };

  const handleConfirmDelete = async () => {
    if (!deleteModalState.targetId) return;
    
    if (deleteModalState.type === 'reply') {
      try {
        await deleteRoomingHouseReviewReply(deleteModalState.targetId);
        await refreshCurrentList();
      } catch (err: any) {
        setToast({ message: getApiErrorMessage(err, 'Lỗi khi xóa phản hồi.'), type: 'error' });
      }
    } else if (deleteModalState.type === 'review') {
      try {
        await deleteRoomingHouseReview(deleteModalState.targetId);
        await refreshCurrentList();
        checkEligibility();
        setToast({ message: 'Đã xóa bài đánh giá thành công.', type: 'success' });
      } catch (err: any) {
        setToast({ message: getApiErrorMessage(err, 'Lỗi khi xóa bài đánh giá.'), type: 'error' });
      }
    }
    
    setDeleteModalState({ isOpen: false, type: null, targetId: null });
  };

  const submitReport = async (reviewId: string) => {
    if (!reportReason.trim()) return;
    try {
      setIsSubmittingReport(true);
      await reportRoomingHouseReview(reviewId, { reason: reportReason });
      setReportingId(null);
      setReportReason('');
      setToast({ message: 'Đã gửi báo cáo thành công. Cảm ơn bạn!', type: 'success' });
      refreshCurrentList();
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Lỗi khi gửi báo cáo.'), type: 'error' });
    } finally {
      setIsSubmittingReport(false);
    }
  };

  const formatDate = (value?: string | null) => {
    if (!value) return 'Chưa có';
    return new Date(value).toLocaleDateString('vi-VN');
  };

  if (loading && !reviewsData) return <div>Đang tải đánh giá...</div>;
  if (error) return <div style={{ color: 'red' }}>{error}</div>;

  const hasReviews = reviewsData && reviewsData.reviews.length > 0;
  const reviewableContracts = reviewEligibility?.reviewableContracts ?? [];
  const contractsAvailableForReview = reviewableContracts.filter(contract => contract.canReview);

  return (
    <div className="house-reviews-list">
      {hasReviews ? (
        <div className="reviews-summary">
          <h3>{reviewsData!.averageRating.toFixed(1)} / 5 sao</h3>
          <p>Dựa trên {reviewsData!.totalReviews} đánh giá</p>
        </div>
      ) : (
        <div className="reviews-summary">
          <p>Chưa có đánh giá nào cho khu trọ này.</p>
        </div>
      )}
      <div id="review-section" className="contract-review-section">
        {!currentUser ? (
          <InlineReviewForm
            mode="create"
            disabled={true}
            disabledReason="Đăng nhập bằng tài khoản người thuê để viết đánh giá."
            displayName="Khách"
            onSuccess={() => {}}
          />
        ) : isLandlord ? (
          <div className="reviews-summary contract-review-notice">
            <h3>Chủ trọ không thể tự đánh giá khu trọ của mình</h3>
            <p>Bạn vẫn có thể phản hồi hoặc báo cáo các đánh giá của người thuê bên dưới.</p>
          </div>
        ) : isCheckingEligibility ? (
          <div className="reviews-summary contract-review-notice">
            <p>Đang kiểm tra quyền đánh giá...</p>
          </div>
        ) : contractsAvailableForReview.length > 0 ? (
          <div className="contract-review-panel">
            <div className="contract-review-panel__header">
              <h3>Đánh giá theo hợp đồng</h3>
              <p>Mỗi hợp đồng đủ điều kiện tại khu trọ này được gửi một đánh giá riêng.</p>
            </div>

            <div className="contract-review-grid">
              {contractsAvailableForReview.map(contract => {
                const review = contract.review;
                const isCreating = activeReviewContractId === contract.contractId;

                return (
                  <article key={contract.contractId} className="contract-review-card">
                    <div className="contract-review-card__main">
                      <div>
                        <strong>Phòng {contract.roomNumber}</strong>
                        <p>{formatDate(contract.startDate)} - {formatDate(contract.endDate)}</p>
                      </div>
                    </div>

                    {isCreating && (
                      <InlineReviewForm
                        mode="create"
                        contractId={contract.contractId}
                        hideAvatar={true}
                        displayName={currentUser?.displayName || currentUser?.email?.split('@')[0] || 'User'}
                        onSuccess={() => {
                          setActiveReviewContractId(null);
                          checkEligibility();
                          refreshCurrentList();
                          setToast({ message: 'Đã gửi đánh giá. Hệ thống đang kiểm duyệt trước khi hiển thị công khai.', type: 'success' });
                        }}
                        onCancel={() => setActiveReviewContractId(null)}
                      />
                    )}

                    {!isCreating && (
                      <div className="contract-review-card__actions">
                        <Button
                          onClick={() => {
                            setEditingReviewId(null);
                            setActiveReviewContractId(contract.contractId);
                          }}
                        >
                          Viết đánh giá
                        </Button>
                      </div>
                    )}
                  </article>
                );
              })}
            </div>
          </div>
        ) : (
          <div className="review-info-box">
            <p>{reviewEligibility?.reason ?? 'Chỉ những người đã thuê trọ tại đây mới được viết đánh giá.'}</p>
          </div>
        )}
      </div>

      {hasReviews && (
        <div className="reviews-items">
        {reviewsData.reviews.map(review => (
          <div key={review.id} id={`review-item-${review.id}`} className="review-item">
            <div className="review-header">
              <div className="review-user">
                {review.tenantAvatarUrl ? (
                  <>
                    <img 
                      src={toAssetUrl(review.tenantAvatarUrl)} 
                      alt={review.tenantDisplayName} 
                      className="review-avatar" 
                      onError={(e) => {
                        e.currentTarget.style.display = 'none';
                        if (e.currentTarget.nextElementSibling) {
                          (e.currentTarget.nextElementSibling as HTMLElement).style.display = 'flex';
                        }
                      }}
                    />
                    <div className="review-avatar-placeholder" style={{ display: 'none' }}>
                      {review.tenantDisplayName.charAt(0).toUpperCase()}
                    </div>
                  </>
                ) : (
                  <div className="review-avatar-placeholder">
                    {review.tenantDisplayName.charAt(0).toUpperCase()}
                  </div>
                )}
                <span className="review-tenant-name">{review.tenantDisplayName}</span>
                <span className="review-date">{new Date(review.createdAt).toLocaleDateString('vi-VN')}</span>
                {review.updatedAt && <span className="review-edited">(Đã chỉnh sửa)</span>}
                {review.roomNumber && (
                  <span className="review-contract-chip">
                    Phòng {review.roomNumber}
                    {review.contractStartDate && review.contractEndDate
                      ? ` • ${formatDate(review.contractStartDate)} - ${formatDate(review.contractEndDate)}`
                      : ''}
                  </span>
                )}
              </div>
              
              <div className="review-header-right" style={{ display: 'flex', alignItems: 'center' }}>
                {editingReviewId !== review.id && (
                  <div className="review-rating">
                    {'★'.repeat(review.rating)}{'☆'.repeat(5 - review.rating)}
                  </div>
                )}
                
                {currentUser && currentUser.userId === review.tenantUserId && editingReviewId !== review.id && (
                  <div className="reply-actions" style={{ marginLeft: '12px' }}>
                    <button 
                      className="icon-btn edit-icon" 
                      onClick={() => setEditingReviewId(review.id)}
                      title="Sửa"
                    >
                      ✎
                    </button>
                    <button 
                      className="icon-btn delete-icon" 
                      onClick={() => handleDeleteReviewClick(review.id)}
                      title="Xóa"
                    >
                      ✕
                    </button>
                  </div>
                )}
              </div>
            </div>

            {editingReviewId === review.id ? (
              <InlineReviewForm
                mode="edit"
                review={review}
                hideAvatar={true}
                onSuccess={() => {
                  setEditingReviewId(null);
                  refreshCurrentList();
                  checkEligibility();
                  setToast({ message: 'Đã gửi chỉnh sửa đánh giá. Hệ thống đang kiểm duyệt trước khi hiển thị công khai.', type: 'success' });
                }}
                onCancel={() => setEditingReviewId(null)}
              />
            ) : (
              <>
            
            <p className="review-comment">{review.comment}</p>
            
            {review.images && review.images.length > 0 && (
              <div className="review-images">
                {review.images.slice(0, 2).map((img, index) => (
                  <div key={img.id} className="review-image-wrapper" onClick={() => setActiveImageGallery({ images: review.images!, index })}>
                    <img src={toAssetUrl(img.imageUrl || img.objectKey)} alt="Review" />
                    {index === 1 && review.images!.length > 2 && (
                      <div className="more-images-overlay">
                        +{review.images!.length - 2}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
            
            <div className="review-actions">
              {isLandlord && (
                <>
                  {!review.isReported && (
                    <button className="text-btn report-btn" onClick={() => { setReportingId(review.id); setReplyingToId(null); }}>
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z"></path><line x1="4" y1="22" x2="4" y2="15"></line></svg>
                      Báo cáo vi phạm
                    </button>
                  )}
                  {!review.landlordReply && (
                    <button className="text-btn reply-btn" onClick={() => { setReplyingToId(review.id); setReportingId(null); }}>
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path></svg>
                      Phản hồi
                    </button>
                  )}
                </>
              )}
            </div>
            </>
            )}

            {reportingId === review.id && (
              <div className="report-box">
                <textarea
                  placeholder="Lý do báo cáo vi phạm..."
                  value={reportReason}
                  onChange={e => setReportReason(e.target.value)}
                />
                <div className="box-actions">
                  <Button variant="outline" onClick={() => setReportingId(null)}>Hủy</Button>
                  <Button onClick={() => submitReport(review.id)} disabled={isSubmittingReport}>Gửi báo cáo</Button>
                </div>
              </div>
            )}

            {replyingToId === review.id && !review.landlordReply && (
              <div className="reply-box">
                <textarea
                  placeholder="Nhập phản hồi của bạn..."
                  value={replyContent}
                  onChange={e => setReplyContent(e.target.value)}
                />
                <div className="box-actions">
                  <Button variant="outline" onClick={() => setReplyingToId(null)}>Hủy</Button>
                  <Button onClick={() => submitReply(review.id)} disabled={isSubmittingReply}>Gửi phản hồi</Button>
                </div>
              </div>
            )}

            {review.landlordReply && (
              <div className="review-reply">
                <div className="reply-header">
                  <div className="review-user">
                    {roomingHouseAvatarUrl ? (
                      <>
                        <img 
                          src={toAssetUrl(roomingHouseAvatarUrl)} 
                          alt={roomingHouseName || 'Nhà trọ'} 
                          className="review-avatar reply-avatar" 
                          onError={(e) => {
                            e.currentTarget.style.display = 'none';
                            if (e.currentTarget.nextElementSibling) {
                              (e.currentTarget.nextElementSibling as HTMLElement).style.display = 'flex';
                            }
                          }}
                        />
                        <div className="review-avatar-placeholder reply-avatar" style={{ display: 'none' }}>
                          {(roomingHouseName || 'P').charAt(0).toUpperCase()}
                        </div>
                      </>
                    ) : (
                      <div className="review-avatar-placeholder reply-avatar">
                        {(roomingHouseName || 'P').charAt(0).toUpperCase()}
                      </div>
                    )}
                    <strong className="review-tenant-name">{roomingHouseName || 'Phản hồi từ chủ trọ'}</strong>
                    {review.landlordReplyCreatedAt && (
                      <span className="review-date">{new Date(review.landlordReplyCreatedAt).toLocaleDateString('vi-VN')}</span>
                    )}
                  </div>
                  {isLandlord && replyingToId !== review.id && (
                    <div className="reply-actions">
                      <button 
                        className="icon-btn edit-icon" 
                        onClick={() => { setReplyingToId(review.id); setReplyContent(review.landlordReply!); }}
                        title="Sửa"
                      >
                        ✎
                      </button>
                      <button 
                        className="icon-btn delete-icon" 
                        onClick={() => handleDeleteReplyClick(review.id)}
                        title="Xóa"
                      >
                        ✕
                      </button>
                    </div>
                  )}
                </div>
                <div className="reply-content-wrapper">
                  {replyingToId === review.id ? (
                    <div className="reply-box inline-edit" style={{ padding: 0, border: 'none', marginBottom: 0, background: 'transparent' }}>
                      <textarea
                        placeholder="Nhập phản hồi của bạn..."
                        value={replyContent}
                        onChange={e => setReplyContent(e.target.value)}
                        autoFocus
                      />
                      <div className="box-actions">
                        <Button variant="outline" onClick={() => setReplyingToId(null)}>Hủy</Button>
                        <Button onClick={() => submitReply(review.id)} disabled={isSubmittingReply}>Cập nhật</Button>
                      </div>
                    </div>
                  ) : (
                    <p className="reply-text">{review.landlordReply}</p>
                  )}
                </div>
              </div>
            )}
          </div>
        ))}
      </div>
      )}
      
      {hasMore && (
        <div style={{ textAlign: 'center', marginTop: '16px', marginBottom: '16px' }}>
          <Button 
            variant="outline" 
            onClick={() => {
              const nextPage = page + 1;
              setPage(nextPage);
              loadReviews(nextPage, true);
            }} 
            disabled={isLoadingMore}
          >
            {isLoadingMore ? 'Đang tải...' : 'Xem thêm'}
          </Button>
        </div>
      )}
      {activeImageGallery && (
        <div className="image-gallery-modal" onClick={() => setActiveImageGallery(null)}>
          <button className="gallery-close" onClick={() => setActiveImageGallery(null)}>&times;</button>
          
          {activeImageGallery.images.length > 1 && (
            <button className="gallery-nav prev" onClick={(e) => {
              e.stopPropagation();
              setActiveImageGallery(prev => prev ? { ...prev, index: (prev.index - 1 + prev.images.length) % prev.images.length } : null);
            }}>
              &#10094;
            </button>
          )}
          
          <img 
            src={toAssetUrl(activeImageGallery.images[activeImageGallery.index].imageUrl || activeImageGallery.images[activeImageGallery.index].objectKey)} 
            alt="Review gallery" 
            onClick={(e) => e.stopPropagation()} 
          />
          
          {activeImageGallery.images.length > 1 && (
            <button className="gallery-nav next" onClick={(e) => {
              e.stopPropagation();
              setActiveImageGallery(prev => prev ? { ...prev, index: (prev.index + 1) % prev.images.length } : null);
            }}>
              &#10095;
            </button>
          )}
          
          <div className="gallery-counter">
            {activeImageGallery.index + 1} / {activeImageGallery.images.length}
          </div>
        </div>
      )}
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}

      <ConfirmModal
        isOpen={deleteModalState.isOpen}
        title={deleteModalState.type === 'review' ? 'Xóa bài đánh giá' : 'Xóa phản hồi'}
        message={
          deleteModalState.type === 'review'
            ? 'Nếu bạn xóa đánh giá này, đánh giá sẽ không còn hiển thị công khai và bạn sẽ không thể viết lại đánh giá cho hợp đồng này. Bạn có chắc chắn muốn tiếp tục?'
            : 'Bạn có chắc chắn muốn xóa phản hồi này không?'
        }
        confirmText="Xóa"
        isDanger={true}
        onConfirm={handleConfirmDelete}
        onCancel={() => setDeleteModalState({ isOpen: false, type: null, targetId: null })}
      />
    </div>
  );
};

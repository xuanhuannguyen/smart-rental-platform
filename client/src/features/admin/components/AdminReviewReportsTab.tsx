import React, { useCallback, useEffect, useState } from "react";
import { adminCatalogApi } from "../services/adminCatalogApi";
import { toAssetUrl } from "../../../shared/api/assets";
import type {
  AdminReviewModerationItemResponse,
  AdminReviewReportResponse,
  PagedResult,
} from "../types/adminCatalog.types";

type Mode = "moderation" | "reports";
type ReportStatusFilter = "" | "Pending" | "Resolved" | "Dismissed";
type ModerationStatusFilter = "PendingAdminReview" | "Approved" | "Rejected";

export const AdminReviewReportsTab: React.FC = () => {
  const [mode, setMode] = useState<Mode>("moderation");
  const [page, setPage] = useState(1);
  const [reportStatus, setReportStatus] = useState<ReportStatusFilter>("Pending");
  const [moderationStatus, setModerationStatus] = useState<ModerationStatusFilter>("PendingAdminReview");
  const [reports, setReports] = useState<PagedResult<AdminReviewReportResponse> | null>(null);
  const [reviews, setReviews] = useState<PagedResult<AdminReviewModerationItemResponse> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [modal, setModal] = useState<
    | { type: "moderate"; review: AdminReviewModerationItemResponse; action: "Approve" | "Reject" }
    | { type: "report"; report: AdminReviewReportResponse }
    | null
  >(null);
  const [note, setNote] = useState("");
  const [hideReview, setHideReview] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [imageDialog, setImageDialog] = useState<{ images: AdminReviewModerationItemResponse["images"]; index: number; title: string } | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      if (mode === "moderation") {
        const res = await adminCatalogApi.getReviewModerationItems(page, 10, moderationStatus);
        setReviews(res.data ?? null);
      } else {
        const res = await adminCatalogApi.getReviewReports(page, 10, reportStatus || undefined);
        setReports(res.data ?? null);
      }
    } catch (e: any) {
      setError(e?.message ?? "Không tải được dữ liệu duyệt đánh giá.");
    } finally {
      setLoading(false);
    }
  }, [mode, page, reportStatus, moderationStatus]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const openModeration = (review: AdminReviewModerationItemResponse, action: "Approve" | "Reject") => {
    setModal({ type: "moderate", review, action });
    setNote("");
  };

  const openReport = (report: AdminReviewReportResponse) => {
    setModal({ type: "report", report });
    setNote("");
    setHideReview(false);
  };

  const submitModal = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!modal) return;

    setSubmitting(true);
    try {
      if (modal.type === "moderate") {
        await adminCatalogApi.moderateReview(modal.review.id, modal.action, note.trim() || undefined);
      } else {
        await adminCatalogApi.resolveReviewReport(modal.report.id, {
          adminNote: note.trim(),
          hideReview,
        });
      }
      setModal(null);
      await loadData();
    } catch (e: any) {
      setError(e?.message ?? "Không xử lý được đánh giá.");
    } finally {
      setSubmitting(false);
    }
  };

  const pageInfo = mode === "moderation" ? reviews : reports;

  return (
    <div style={{ padding: "0 0 40px" }}>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 12, marginBottom: 18, flexWrap: "wrap" }}>
        <div style={{ display: "flex", gap: 8 }}>
          <button type="button" onClick={() => { setMode("moderation"); setPage(1); }} style={tabBtn(mode === "moderation")}>
            Duyệt đánh giá
          </button>
          <button type="button" onClick={() => { setMode("reports"); setPage(1); }} style={tabBtn(mode === "reports")}>
            Báo cáo từ chủ trọ
          </button>
        </div>

        {mode === "moderation" ? (
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            {(["PendingAdminReview", "Approved", "Rejected"] as ModerationStatusFilter[]).map((status) => (
              <button key={status} type="button" onClick={() => { setModerationStatus(status); setPage(1); }} style={filterBtn(moderationStatus === status)}>
                {moderationLabel(status)}
              </button>
            ))}
          </div>
        ) : (
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            {(["", "Pending", "Resolved", "Dismissed"] as ReportStatusFilter[]).map((status) => (
              <button key={status || "all"} type="button" onClick={() => { setReportStatus(status); setPage(1); }} style={filterBtn(reportStatus === status)}>
                {status === "" ? "Tất cả" : reportLabel(status)}
              </button>
            ))}
          </div>
        )}
      </div>

      {error && <p style={{ color: "#dc2626" }}>{error}</p>}
      {loading && <p style={{ color: "#64748b" }}>Đang tải...</p>}

      {!loading && mode === "moderation" && reviews && (
        <ReviewModerationTable
          items={reviews.items}
          onAction={openModeration}
          onOpenImages={(review) => setImageDialog({ images: review.images, index: 0, title: `${review.roomingHouseName} - ${review.tenantDisplayName}` })}
        />
      )}

      {!loading && mode === "reports" && reports && (
        <ReportTable items={reports.items} onResolve={openReport} />
      )}

      {!loading && pageInfo && pageInfo.items.length === 0 && (
        <p style={{ color: "#64748b" }}>Không có dữ liệu.</p>
      )}

      {pageInfo && pageInfo.totalPages > 1 && (
        <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 16 }}>
          <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)} style={pagBtn}>Trước</button>
          <span style={{ padding: "6px 10px", fontSize: "0.875rem", color: "#475569" }}>
            Trang {page} / {pageInfo.totalPages}
          </span>
          <button disabled={page >= pageInfo.totalPages} onClick={() => setPage((p) => p + 1)} style={pagBtn}>Tiếp</button>
        </div>
      )}

      {modal && (
        <div style={overlay} onClick={(e) => e.target === e.currentTarget && setModal(null)}>
          <form onSubmit={submitModal} style={dialog}>
            <h3 style={{ margin: "0 0 8px", color: "#0f172a" }}>
              {modal.type === "moderate"
                ? modal.action === "Approve" ? "Duyệt hiển thị đánh giá" : "Từ chối đánh giá"
                : "Xử lý báo cáo đánh giá"}
            </h3>
            <p style={{ color: "#475569", margin: "0 0 14px", fontSize: "0.9rem" }}>
              {modal.type === "moderate"
                ? `${modal.review.roomingHouseName} - ${modal.review.tenantDisplayName}`
                : `${modal.report.roomingHouseName ?? "Khu trọ"} - ${modal.report.reason}`}
            </p>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              rows={3}
              required={modal.type === "report" || (modal.type === "moderate" && modal.action === "Reject")}
              style={textarea}
              placeholder="Ghi chú xử lý..."
            />
            {modal.type === "report" && (
              <label style={{ display: "flex", alignItems: "center", gap: 8, margin: "12px 0 18px", cursor: "pointer" }}>
                <input type="checkbox" checked={hideReview} onChange={(e) => setHideReview(e.target.checked)} />
                <span style={{ fontSize: "0.9rem" }}>Ẩn đánh giá bị báo cáo</span>
              </label>
            )}
            <div style={{ display: "flex", justifyContent: "flex-end", gap: 10, marginTop: 18 }}>
              <button type="button" onClick={() => setModal(null)} disabled={submitting} style={{ ...actionBtn, background: "#f1f5f9", color: "#334155" }}>
                Hủy
              </button>
              <button type="submit" disabled={submitting} style={{ ...actionBtn, background: "#1d4ed8", color: "#fff" }}>
                {submitting ? "Đang xử lý..." : "Xác nhận"}
              </button>
            </div>
          </form>
        </div>
      )}

      {imageDialog && (
        <div style={overlay} onClick={(e) => e.target === e.currentTarget && setImageDialog(null)}>
          <div style={imageDialogStyle}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, marginBottom: 12 }}>
              <div>
                <h3 style={{ margin: 0, color: "#0f172a" }}>Ảnh đánh giá</h3>
                <p style={{ margin: "4px 0 0", color: "#64748b", fontSize: "0.88rem" }}>{imageDialog.title}</p>
              </div>
              <button type="button" onClick={() => setImageDialog(null)} style={closeBtn}>×</button>
            </div>
            <div style={imageStage}>
              {imageDialog.images.length > 1 && (
                <button
                  type="button"
                  style={{ ...galleryNav, left: 12 }}
                  onClick={() => setImageDialog((prev) => prev ? { ...prev, index: (prev.index - 1 + prev.images.length) % prev.images.length } : null)}
                >
                  ‹
                </button>
              )}
              <img
                src={toAssetUrl(imageDialog.images[imageDialog.index].imageUrl)}
                alt="Ảnh đánh giá"
                style={galleryImage}
              />
              {imageDialog.images.length > 1 && (
                <button
                  type="button"
                  style={{ ...galleryNav, right: 12 }}
                  onClick={() => setImageDialog((prev) => prev ? { ...prev, index: (prev.index + 1) % prev.images.length } : null)}
                >
                  ›
                </button>
              )}
            </div>
            <div style={{ display: "flex", justifyContent: "center", gap: 8, marginTop: 12 }}>
              {imageDialog.images.map((image, index) => (
                <button
                  key={image.id}
                  type="button"
                  onClick={() => setImageDialog((prev) => prev ? { ...prev, index } : null)}
                  style={{
                    ...galleryDot,
                    borderColor: index === imageDialog.index ? "#2563eb" : "#cbd5e1",
                    opacity: index === imageDialog.index ? 1 : 0.65,
                  }}
                >
                  <img src={toAssetUrl(image.imageUrl)} alt="" style={mediaImage} />
                </button>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

function ReviewModerationTable({
  items,
  onAction,
  onOpenImages,
}: {
  items: AdminReviewModerationItemResponse[];
  onAction: (review: AdminReviewModerationItemResponse, action: "Approve" | "Reject") => void;
  onOpenImages: (review: AdminReviewModerationItemResponse) => void;
}) {
  if (items.length === 0) return null;
  return (
    <div style={{ overflowX: "auto" }}>
      <table style={table}>
        <thead>
          <tr style={headRow}>
            <th style={th}>Khu trọ</th>
            <th style={th}>Người đánh giá</th>
            <th style={th}>Đánh giá</th>
            <th style={th}>Ảnh</th>
            <th style={th}>AI</th>
            <th style={th}>Trạng thái</th>
            <th style={th}>Hành động</th>
          </tr>
        </thead>
        <tbody>
          {items.map((review) => (
            <tr key={review.id} style={row}>
              <td style={td}>{review.roomingHouseName}</td>
              <td style={td}>{review.tenantDisplayName}</td>
              <td style={{ ...td, maxWidth: 320 }}>
                <strong style={{ color: "#f59e0b" }}>{"★".repeat(review.rating)}{"☆".repeat(5 - review.rating)}</strong>
                <p style={{ margin: "6px 0 0", color: "#334155" }}>{review.comment || "Không có nội dung."}</p>
              </td>
              <td style={{ ...td, minWidth: 150 }}>
                {review.images && review.images.length > 0 ? (
                  <button type="button" onClick={() => onOpenImages(review)} style={smallBtn("#eff6ff", "#1d4ed8")}>
                    Xem ảnh ({review.images.length})
                  </button>
                ) : (
                  <span style={{ color: "#94a3b8" }}>Không có ảnh</span>
                )}
              </td>
              <td style={td}>
                {review.aiContentComment || review.aiImageComment ? (
                  <>
                    {review.aiContentComment && (
                      <p style={aiNote}><strong>Nội dung:</strong> {review.aiContentComment}</p>
                    )}
                    {review.aiImageComment && (
                      <p style={aiNote}><strong>Hình ảnh:</strong> {review.aiImageComment}</p>
                    )}
                  </>
                ) : (
                  <span style={{ color: "#94a3b8" }}>Chưa có nhận xét AI</span>
                )}
              </td>
              <td style={td}>{statusBadge(review.moderationStatus)}</td>
              <td style={td}>
                {review.moderationStatus === "PendingAdminReview" ? (
                  <div style={{ display: "flex", gap: 8 }}>
                    <button type="button" onClick={() => onAction(review, "Approve")} style={smallBtn("#dcfce7", "#166534")}>Duyệt</button>
                    <button type="button" onClick={() => onAction(review, "Reject")} style={smallBtn("#fee2e2", "#991b1b")}>Từ chối</button>
                  </div>
                ) : (
                  <span style={{ color: "#64748b" }}>Đã xử lý</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ReportTable({
  items,
  onResolve,
}: {
  items: AdminReviewReportResponse[];
  onResolve: (report: AdminReviewReportResponse) => void;
}) {
  if (items.length === 0) return null;
  return (
    <div style={{ overflowX: "auto" }}>
      <table style={table}>
        <thead>
          <tr style={headRow}>
            <th style={th}>Người báo cáo</th>
            <th style={th}>Khu trọ</th>
            <th style={th}>Đánh giá</th>
            <th style={th}>Lý do</th>
            <th style={th}>Trạng thái</th>
            <th style={th}>Hành động</th>
          </tr>
        </thead>
        <tbody>
          {items.map((report) => {
            const reporter = report.reporterName || report.reporterDisplayName || "—";
            const comment = report.reviewContent || report.review?.comment || "—";
            return (
              <tr key={report.id} style={row}>
                <td style={td}>{reporter}</td>
                <td style={td}>{report.roomingHouseName ?? "—"}</td>
                <td style={{ ...td, maxWidth: 300 }}>{comment}</td>
                <td style={td}>{report.reason}</td>
                <td style={td}>{statusBadge(report.status)}</td>
                <td style={td}>
                  {report.status === "Pending" ? (
                    <button type="button" onClick={() => onResolve(report)} style={smallBtn("#eff6ff", "#1d4ed8")}>Xử lý</button>
                  ) : (
                    <span style={{ color: "#64748b" }}>{report.adminNote || report.resolution || "Đã xử lý"}</span>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function moderationLabel(status: ModerationStatusFilter) {
  if (status === "PendingAdminReview") return "Chờ admin";
  if (status === "Approved") return "Đã duyệt";
  return "Từ chối";
}

function reportLabel(status: Exclude<ReportStatusFilter, "">) {
  if (status === "Pending") return "Chờ xử lý";
  if (status === "Resolved") return "Đã xử lý";
  return "Bỏ qua";
}

function statusBadge(status: string) {
  const color = status === "Approved" || status === "Resolved"
    ? { bg: "#dcfce7", fg: "#166534" }
    : status === "PendingAdminReview" || status === "Pending"
      ? { bg: "#fef9c3", fg: "#854d0e" }
      : status === "Rejected"
        ? { bg: "#fee2e2", fg: "#991b1b" }
        : { bg: "#f1f5f9", fg: "#475569" };

  const label = status === "PendingAdminReview" ? "Chờ admin" : status === "Approved" ? "Đã duyệt" : status === "Rejected" ? "Từ chối" : reportLabel((status as Exclude<ReportStatusFilter, "">) || "Dismissed");

  return <span style={{ background: color.bg, color: color.fg, padding: "3px 10px", borderRadius: 999, fontSize: "0.78rem", fontWeight: 700 }}>{label}</span>;
}

const tabBtn = (active: boolean): React.CSSProperties => ({
  padding: "8px 16px",
  borderRadius: 8,
  border: `1px solid ${active ? "#1d4ed8" : "#cbd5e1"}`,
  background: active ? "#1d4ed8" : "#fff",
  color: active ? "#fff" : "#334155",
  cursor: "pointer",
  fontWeight: 700,
});

const filterBtn = (active: boolean): React.CSSProperties => ({
  padding: "7px 13px",
  borderRadius: 8,
  border: `1px solid ${active ? "#2563eb" : "#cbd5e1"}`,
  background: active ? "#eff6ff" : "#fff",
  color: active ? "#1d4ed8" : "#334155",
  cursor: "pointer",
  fontWeight: active ? 700 : 500,
});

const smallBtn = (bg: string, color: string): React.CSSProperties => ({
  padding: "5px 11px",
  borderRadius: 7,
  border: "1px solid transparent",
  background: bg,
  color,
  cursor: "pointer",
  fontWeight: 700,
  fontSize: "0.82rem",
});

const table: React.CSSProperties = { width: "100%", borderCollapse: "collapse", fontSize: "0.9rem" };
const headRow: React.CSSProperties = { background: "#f8fafc", borderBottom: "2px solid #e2e8f0" };
const row: React.CSSProperties = { borderBottom: "1px solid #f1f5f9" };
const th: React.CSSProperties = { padding: "10px 12px", textAlign: "left", fontWeight: 700, color: "#334155", fontSize: "0.82rem", textTransform: "uppercase" };
const td: React.CSSProperties = { padding: "10px 12px", color: "#374151", verticalAlign: "middle" };
const pagBtn: React.CSSProperties = { padding: "6px 14px", borderRadius: 6, border: "1px solid #cbd5e1", background: "#fff", cursor: "pointer" };
const actionBtn: React.CSSProperties = { padding: "8px 20px", borderRadius: 8, border: 0, cursor: "pointer", fontWeight: 700 };
const overlay: React.CSSProperties = { position: "fixed", inset: 0, background: "rgba(15,23,42,0.55)", zIndex: 1200, display: "flex", alignItems: "center", justifyContent: "center", padding: 20 };
const dialog: React.CSSProperties = { background: "#fff", borderRadius: 14, padding: 26, width: "min(520px,100%)", boxShadow: "0 20px 60px rgba(15,23,42,0.25)" };
const textarea: React.CSSProperties = { width: "100%", boxSizing: "border-box", border: "1px solid #cbd5e1", borderRadius: 8, padding: "9px 12px", fontSize: "0.9rem", resize: "vertical" };
const aiNote: React.CSSProperties = { margin: "6px 0 0", color: "#334155", fontSize: "0.82rem", lineHeight: 1.45 };
const mediaImage: React.CSSProperties = { width: "100%", height: "100%", objectFit: "cover", display: "block" };
const imageDialogStyle: React.CSSProperties = { background: "#fff", borderRadius: 14, padding: 18, width: "min(760px,100%)", boxShadow: "0 20px 60px rgba(15,23,42,0.25)" };
const imageStage: React.CSSProperties = { position: "relative", height: "min(64vh,520px)", background: "#0f172a", borderRadius: 12, overflow: "hidden", display: "flex", alignItems: "center", justifyContent: "center" };
const galleryImage: React.CSSProperties = { maxWidth: "100%", maxHeight: "100%", objectFit: "contain" };
const galleryNav: React.CSSProperties = { position: "absolute", top: "50%", transform: "translateY(-50%)", width: 40, height: 40, borderRadius: 999, border: 0, background: "rgba(255,255,255,0.88)", color: "#0f172a", fontSize: 30, lineHeight: "36px", cursor: "pointer" };
const galleryDot: React.CSSProperties = { width: 58, height: 58, borderRadius: 8, overflow: "hidden", border: "2px solid #cbd5e1", padding: 0, background: "#f8fafc", cursor: "pointer" };
const closeBtn: React.CSSProperties = { width: 34, height: 34, borderRadius: 999, border: "1px solid #cbd5e1", background: "#fff", color: "#334155", fontSize: 22, cursor: "pointer" };

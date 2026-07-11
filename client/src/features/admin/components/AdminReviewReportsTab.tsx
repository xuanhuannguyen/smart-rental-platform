import React, { useState, useEffect, useCallback } from "react";
import { adminCatalogApi } from "../services/adminCatalogApi";
import type {
  AdminReviewReportResponse,
  PagedResult,
} from "../types/adminCatalog.types";

type StatusFilter = "" | "Pending" | "Resolved" | "Dismissed";

export const AdminReviewReportsTab: React.FC = () => {
  const [reports, setReports] = useState<PagedResult<AdminReviewReportResponse> | null>(null);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("Pending");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Resolve dialog state
  const [resolving, setResolving] = useState<AdminReviewReportResponse | null>(null);
  const [resolution, setResolution] = useState("");
  const [hideReview, setHideReview] = useState(false);
  const [resolveLoading, setResolveLoading] = useState(false);
  const [resolveError, setResolveError] = useState<string | null>(null);

  const loadReports = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await adminCatalogApi.getReviewReports(page, 10, statusFilter || undefined);
      setReports(res.data ?? null);
    } catch (e: any) {
      setError(e?.message ?? "Không tải được danh sách báo cáo.");
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter]);

  useEffect(() => {
    loadReports();
  }, [loadReports]);

  const handleResolveOpen = (report: AdminReviewReportResponse) => {
    setResolving(report);
    setResolution("");
    setHideReview(false);
    setResolveError(null);
  };

  const handleResolveSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!resolving) return;
    setResolveLoading(true);
    setResolveError(null);
    try {
      await adminCatalogApi.resolveReviewReport(resolving.id, {
        resolution: resolution.trim(),
        hideReview,
      });
      setResolving(null);
      loadReports();
    } catch (e: any) {
      setResolveError(e?.message ?? "Có lỗi xảy ra khi xử lý báo cáo.");
    } finally {
      setResolveLoading(false);
    }
  };

  const statusBadge = (status: string) => {
    const colors: Record<string, { bg: string; color: string }> = {
      Pending: { bg: "#fef9c3", color: "#854d0e" },
      Resolved: { bg: "#dcfce7", color: "#166534" },
      Dismissed: { bg: "#f1f5f9", color: "#475569" },
    };
    const s = colors[status] ?? { bg: "#f1f5f9", color: "#475569" };
    return (
      <span
        style={{
          background: s.bg,
          color: s.color,
          padding: "2px 10px",
          borderRadius: 12,
          fontSize: "0.78rem",
          fontWeight: 600,
        }}
      >
        {status === "Pending" ? "Chờ xử lý" : status === "Resolved" ? "Đã xử lý" : "Bỏ qua"}
      </span>
    );
  };

  return (
    <div style={{ padding: "0 0 40px" }}>
      {/* Filters */}
      <div style={{ display: "flex", gap: 10, marginBottom: 20, flexWrap: "wrap" }}>
        {(["", "Pending", "Resolved", "Dismissed"] as StatusFilter[]).map((s) => (
          <button
            key={s}
            id={`admin-review-reports-filter-${s || "all"}`}
            onClick={() => { setStatusFilter(s); setPage(1); }}
            style={{
              padding: "6px 16px",
              borderRadius: 8,
              border: "1px solid",
              cursor: "pointer",
              fontWeight: statusFilter === s ? 700 : 400,
              background: statusFilter === s ? "#1d4ed8" : "#fff",
              color: statusFilter === s ? "#fff" : "#334155",
              borderColor: statusFilter === s ? "#1d4ed8" : "#cbd5e1",
            }}
          >
            {s === "" ? "Tất cả" : s === "Pending" ? "Chờ xử lý" : s === "Resolved" ? "Đã xử lý" : "Bỏ qua"}
          </button>
        ))}
      </div>

      {loading && <p style={{ color: "#64748b" }}>Đang tải...</p>}
      {error && <p style={{ color: "#dc2626" }}>{error}</p>}

      {!loading && reports && reports.items.length === 0 && (
        <p style={{ color: "#64748b" }}>Không có báo cáo nào.</p>
      )}

      {!loading && reports && reports.items.length > 0 && (
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.9rem" }}>
            <thead>
              <tr style={{ background: "#f8fafc", borderBottom: "2px solid #e2e8f0" }}>
                <th style={th}>Người báo cáo</th>
                <th style={th}>Nhà trọ</th>
                <th style={th}>Đánh giá</th>
                <th style={th}>Lý do</th>
                <th style={th}>Trạng thái</th>
                <th style={th}>Ngày tạo</th>
                <th style={th}>Hành động</th>
              </tr>
            </thead>
            <tbody>
              {reports.items.map((r) => (
                <tr key={r.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={td}>{r.reporterName}</td>
                  <td style={td}>{r.roomingHouseName ?? "—"}</td>
                  <td style={{ ...td, maxWidth: 200, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {r.reviewContent ?? "—"}
                  </td>
                  <td style={td}>{r.reason}</td>
                  <td style={td}>{statusBadge(r.status)}</td>
                  <td style={td}>{new Date(r.createdAt).toLocaleDateString("vi-VN")}</td>
                  <td style={td}>
                    {r.status === "Pending" && (
                      <button
                        id={`admin-resolve-report-${r.id}`}
                        onClick={() => handleResolveOpen(r)}
                        style={{
                          padding: "4px 12px",
                          borderRadius: 6,
                          border: "1px solid #2563eb",
                          background: "#eff6ff",
                          color: "#1d4ed8",
                          cursor: "pointer",
                          fontSize: "0.82rem",
                          fontWeight: 600,
                        }}
                      >
                        Xử lý
                      </button>
                    )}
                    {r.status !== "Pending" && r.resolution && (
                      <span style={{ color: "#64748b", fontSize: "0.82rem" }}>{r.resolution}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pagination */}
          <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 16 }}>
            <button
              id="admin-review-reports-prev"
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
              style={pagBtn}
            >
              &lsaquo; Trước
            </button>
            <span style={{ padding: "6px 10px", fontSize: "0.875rem", color: "#475569" }}>
              Trang {page} / {reports.totalPages}
            </span>
            <button
              id="admin-review-reports-next"
              disabled={page >= reports.totalPages}
              onClick={() => setPage((p) => p + 1)}
              style={pagBtn}
            >
              Tiếp &rsaquo;
            </button>
          </div>
        </div>
      )}

      {/* Resolve Dialog */}
      {resolving && (
        <div
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(15,23,42,0.55)",
            zIndex: 1200,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            padding: 20,
          }}
          onClick={(e) => e.target === e.currentTarget && setResolving(null)}
        >
          <div
            style={{
              background: "#fff",
              borderRadius: 14,
              padding: 28,
              width: "min(480px,100%)",
              boxShadow: "0 20px 60px rgba(15,23,42,0.25)",
            }}
          >
            <h3 style={{ marginTop: 0, marginBottom: 8, color: "#0f172a" }}>Xử lý báo cáo</h3>
            <p style={{ color: "#475569", marginBottom: 16, fontSize: "0.9rem" }}>
              Báo cáo từ: <strong>{resolving.reporterName}</strong> — Lý do:{" "}
              <strong>{resolving.reason}</strong>
            </p>
            {resolveError && (
              <p style={{ color: "#dc2626", fontSize: "0.875rem", margin: "0 0 12px" }}>{resolveError}</p>
            )}
            <form onSubmit={handleResolveSubmit}>
              <div style={{ marginBottom: 14 }}>
                <label style={{ display: "block", fontWeight: 600, marginBottom: 6, fontSize: "0.875rem" }}>
                  Ghi chú xử lý *
                </label>
                <textarea
                  id="admin-resolve-resolution-text"
                  value={resolution}
                  onChange={(e) => setResolution(e.target.value)}
                  rows={3}
                  required
                  style={{
                    width: "100%",
                    boxSizing: "border-box",
                    border: "1px solid #cbd5e1",
                    borderRadius: 8,
                    padding: "8px 12px",
                    fontSize: "0.9rem",
                    resize: "vertical",
                  }}
                  placeholder="Mô tả cách xử lý..."
                />
              </div>
              <label style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 20, cursor: "pointer" }}>
                <input
                  id="admin-resolve-hide-review"
                  type="checkbox"
                  checked={hideReview}
                  onChange={(e) => setHideReview(e.target.checked)}
                />
                <span style={{ fontSize: "0.9rem" }}>Ẩn đánh giá bị báo cáo</span>
              </label>
              <div style={{ display: "flex", justifyContent: "flex-end", gap: 10 }}>
                <button
                  type="button"
                  onClick={() => setResolving(null)}
                  disabled={resolveLoading}
                  style={{ ...actionBtn, background: "#f1f5f9", color: "#334155" }}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={resolveLoading || !resolution.trim()}
                  style={{ ...actionBtn, background: "#1d4ed8", color: "#fff" }}
                >
                  {resolveLoading ? "Đang xử lý..." : "Xác nhận"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

// Inline style helpers
const th: React.CSSProperties = {
  padding: "10px 12px",
  textAlign: "left",
  fontWeight: 600,
  color: "#334155",
  fontSize: "0.82rem",
  textTransform: "uppercase",
  letterSpacing: "0.04em",
};
const td: React.CSSProperties = {
  padding: "10px 12px",
  color: "#374151",
  verticalAlign: "middle",
};
const pagBtn: React.CSSProperties = {
  padding: "5px 14px",
  borderRadius: 6,
  border: "1px solid #cbd5e1",
  background: "#fff",
  cursor: "pointer",
  fontSize: "0.85rem",
};
const actionBtn: React.CSSProperties = {
  padding: "8px 20px",
  borderRadius: 8,
  border: 0,
  cursor: "pointer",
  fontWeight: 600,
  fontSize: "0.9rem",
};

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '../../../app/providers/AuthProvider';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { rentalRequestApi } from '../../rental-requests/api';
import { contractApi } from '../../contracts/api';
import type { RentalRequestResponse } from '../../rental-requests/types';
import type {
  ContractDetailResponse,
  ContractFileResponse,
  ContractOccupantRequest,
  ContractPreviewResponse,
  UpdateContractTermsRequest
} from '../../contracts/types';
import './RentalFlowTestPage.css';

const seedRoomId = '30000000-0000-0000-0000-000000000101';
const seedCoTenantUserId = '10000000-0000-0000-0000-000000000003';

function toDateInput(date: Date) {
  return date.toISOString().slice(0, 10);
}

function addDays(days: number) {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return toDateInput(date);
}

function addHoursIso(hours: number) {
  const date = new Date();
  date.setHours(date.getHours() + hours);
  return date.toISOString();
}

function formatMoney(value?: number | null) {
  if (value == null) {
    return '-';
  }

  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0
  }).format(value);
}

export function RentalFlowTestPage() {
  const { currentUser } = useAuth();
  const [roomId, setRoomId] = useState(seedRoomId);
  const [desiredStartDate, setDesiredStartDate] = useState(addDays(7));
  const [expectedEndDate, setExpectedEndDate] = useState(addDays(97));
  const [expectedOccupantCount, setExpectedOccupantCount] = useState(3);
  const [tenantNote, setTenantNote] = useState('Test rental request from frontend.');
  const [coTenantUserId, setCoTenantUserId] = useState(seedCoTenantUserId);
  const [dependentFullName, setDependentFullName] = useState('Nguyen Van Phu Thuoc');
  const [dependentPhoneNumber, setDependentPhoneNumber] = useState('0900000003');
  const [dependentDateOfBirth, setDependentDateOfBirth] = useState('2010-01-01');
  const [dependentRelationship, setDependentRelationship] = useState('Em ruot');
  const [dependentDocumentType, setDependentDocumentType] = useState('BirthCertificate');
  const [dependentDocumentNumber, setDependentDocumentNumber] = useState('BC123456');
  const [dependentFrontImageObjectKey, setDependentFrontImageObjectKey] = useState('demo/occupants/dependent-front.jpg');
  const [myRequests, setMyRequests] = useState<RentalRequestResponse[]>([]);
  const [incomingRequests, setIncomingRequests] = useState<RentalRequestResponse[]>([]);
  const [contractDetails, setContractDetails] = useState<Record<string, ContractDetailResponse>>({});
  const [contractPreviews, setContractPreviews] = useState<Record<string, ContractPreviewResponse>>({});
  const [contractFiles, setContractFiles] = useState<Record<string, ContractFileResponse[]>>({});
  const [isLoading, setIsLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  const roles = currentUser?.roles ?? [];
  const isTenant = roles.includes('Tenant');

  const loadData = useCallback(async () => {
    if (!currentUser) {
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      const [myResponse, incomingResponse] = await Promise.all([
        rentalRequestApi.getMyRentalRequests(),
        rentalRequestApi.getIncomingRentalRequests()
      ]);

      setMyRequests(myResponse.data);
      setIncomingRequests(incomingResponse.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Khong tai duoc du lieu rental flow.'));
    } finally {
      setIsLoading(false);
    }
  }, [currentUser]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  async function runAction(action: () => Promise<unknown>, successMessage: string) {
    setIsLoading(true);
    setMessage('');
    setError('');

    try {
      await action();
      setMessage(successMessage);
      await loadData();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setIsLoading(false);
    }
  }

  async function requestTenantOtpAndSign(contractId: string) {
    const otpResponse = await contractApi.requestTenantSignOtp(contractId);
    const otp = window.prompt(`Nhap OTP da gui den ${otpResponse.data.maskedEmail}`);

    if (!otp?.trim()) {
      throw new Error('OTP ky hop dong khong duoc de trong.');
    }

    return contractApi.tenantSignContract(contractId, {
      otp: otp.trim(),
      signatureText: currentUser?.displayName ?? currentUser?.email
    });
  }

  async function requestLandlordOtpAndSign(contractId: string) {
    const otpResponse = await contractApi.requestLandlordSignOtp(contractId);
    const otp = window.prompt(`Nhap OTP da gui den ${otpResponse.data.maskedEmail}`);

    if (!otp?.trim()) {
      throw new Error('OTP ky hop dong khong duoc de trong.');
    }

    return contractApi.landlordSignContract(contractId, {
      otp: otp.trim(),
      signatureText: currentUser?.displayName ?? currentUser?.email
    });
  }

  async function createRentalRequest() {
    await runAction(
      () =>
        rentalRequestApi.createRentalRequest(roomId, {
          desiredStartDate,
          expectedEndDate,
          expectedOccupantCount,
          tenantNote
        }),
      'Da tao rental request.'
    );
  }

  async function viewContract(contractId: string) {
    await runAction(async () => {
      const response = await contractApi.getContract(contractId);
      setContractDetails((current) => ({
        ...current,
        [contractId]: response.data
      }));
    }, 'Da tai chi tiet contract.');
  }

  async function viewContractPreview(contractId: string) {
    await runAction(async () => {
      const response = await contractApi.getContractPreview(contractId);
      setContractPreviews((current) => ({
        ...current,
        [contractId]: response.data
      }));
    }, 'Da tai preview contract.');
  }

  async function generateContractFile(contractId: string) {
    await runAction(async () => {
      const response = await contractApi.generateContractFile(contractId);
      setContractFiles((current) => ({
        ...current,
        [contractId]: [response.data, ...(current[contractId] ?? []).filter((file) => file.id !== response.data.id)]
      }));
    }, 'Da tao file PDF hop dong.');
  }

  async function loadContractFiles(contractId: string) {
    await runAction(async () => {
      const response = await contractApi.getContractFiles(contractId);
      setContractFiles((current) => ({
        ...current,
        [contractId]: response.data
      }));
    }, 'Da tai danh sach file hop dong.');
  }

  async function downloadContractFile(contractId: string, fileId: string) {
    await runAction(async () => {
      const blob = await contractApi.downloadContractFile(contractId, fileId);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener,noreferrer');
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    }, 'Da mo file PDF hop dong.');
  }

  function buildOccupants(request: RentalRequestResponse): ContractOccupantRequest[] {
    if (!currentUser) {
      return [];
    }

    return [
      {
        clientReferenceId: 'main-tenant',
        userId: currentUser.userId,
        relationshipToMainTenant: 'Nguoi thue chinh',
        moveInDate: request.desiredStartDate
      },
      {
        clientReferenceId: 'co-tenant',
        userId: coTenantUserId.trim(),
        relationshipToMainTenant: 'Ban cung phong',
        moveInDate: request.desiredStartDate
      },
      {
        clientReferenceId: 'dependent',
        guardianClientReferenceId: 'main-tenant',
        fullName: dependentFullName.trim(),
        phoneNumber: dependentPhoneNumber.trim(),
        dateOfBirth: dependentDateOfBirth,
        relationshipToMainTenant: dependentRelationship.trim(),
        moveInDate: request.desiredStartDate,
        document: {
          documentType: dependentDocumentType.trim(),
          documentNumber: dependentDocumentNumber.trim(),
          frontImageObjectKey: dependentFrontImageObjectKey.trim()
        }
      }
    ];
  }

  async function submitOccupants(request: RentalRequestResponse) {
    if (!request.contract) {
      return;
    }

    await runAction(
      () =>
        contractApi.submitContractOccupants(request.contract!.id, {
          occupants: buildOccupants(request)
        }),
      'Da gui thong tin nguoi o.'
    );
  }

  const tenantHelp = useMemo(() => {
    if (!currentUser) {
      return 'Ban can dang nhap de test.';
    }

    if (!isTenant) {
      return 'Tai khoan hien tai khong co role Tenant; neu backend cho phep thi van co the xem du lieu.';
    }

    return 'Dung room seed de tao rental request, thanh toan coc, submit occupants va test contract signing.';
  }, [currentUser, isTenant]);

  return (
    <div className="rental-test-page">
      <header className="rental-test-header">
        <div>
          <h1>Rental Flow Test</h1>
          <p>Trang test rental request, deposit, contract va signing.</p>
        </div>
        <button className="rental-test-button" disabled={isLoading} onClick={() => void loadData()}>
          Reload
        </button>
      </header>

      {message && <div className="rental-test-message">{message}</div>}
      {error && <div className="rental-test-error">{error}</div>}

      <section className="rental-test-panel" style={{ marginBottom: 16 }}>
        <h2>User hien tai</h2>
        <div className="rental-test-meta">
          <span>Email: {currentUser?.email ?? '-'}</span>
          <span>UserId: {currentUser?.userId ?? '-'}</span>
          <span>Roles: {roles.length > 0 ? roles.join(', ') : '-'}</span>
        </div>
      </section>

      <div className="rental-test-grid">
        <section className="rental-test-panel">
          <h2>Tenant flow</h2>
          <p>{tenantHelp}</p>

          <div className="rental-test-form">
            <label className="full-row">
              RoomId
              <input value={roomId} onChange={(event) => setRoomId(event.target.value)} />
            </label>
            <label>
              Ngay bat dau
              <input type="date" value={desiredStartDate} onChange={(event) => setDesiredStartDate(event.target.value)} />
            </label>
            <label>
              Ngay ket thuc
              <input type="date" value={expectedEndDate} onChange={(event) => setExpectedEndDate(event.target.value)} />
            </label>
            <label>
              So nguoi
              <input
                type="number"
                min={1}
                value={expectedOccupantCount}
                onChange={(event) => setExpectedOccupantCount(Number(event.target.value))}
              />
            </label>
            <label className="full-row">
              Ghi chu
              <textarea value={tenantNote} onChange={(event) => setTenantNote(event.target.value)} />
            </label>
          </div>

          <button className="rental-test-button primary" disabled={isLoading || !currentUser} onClick={() => void createRentalRequest()}>
            Tao rental request
          </button>

          <section className="rental-test-subpanel">
            <h3>Thong tin nguoi o</h3>
            <p>
              Nut submit occupants se gui 3 nguoi: tenant hien tai, co-tenant seed co KYC, va 1 nguoi chua co tai khoan kem giay to.
            </p>
            <div className="rental-test-form">
              <label className="full-row">
                Main tenant userId
                <input value={currentUser?.userId ?? ''} disabled />
              </label>
              <label className="full-row">
                Co-tenant userId
                <input value={coTenantUserId} onChange={(event) => setCoTenantUserId(event.target.value)} />
              </label>
              <label>
                Ho ten nguoi chua co tai khoan
                <input value={dependentFullName} onChange={(event) => setDependentFullName(event.target.value)} />
              </label>
              <label>
                So dien thoai
                <input value={dependentPhoneNumber} onChange={(event) => setDependentPhoneNumber(event.target.value)} />
              </label>
              <label>
                Ngay sinh
                <input
                  type="date"
                  value={dependentDateOfBirth}
                  onChange={(event) => setDependentDateOfBirth(event.target.value)}
                />
              </label>
              <label>
                Quan he voi nguoi thue chinh
                <input value={dependentRelationship} onChange={(event) => setDependentRelationship(event.target.value)} />
              </label>
              <label>
                Loai giay to
                <input value={dependentDocumentType} onChange={(event) => setDependentDocumentType(event.target.value)} />
              </label>
              <label>
                So giay to
                <input value={dependentDocumentNumber} onChange={(event) => setDependentDocumentNumber(event.target.value)} />
              </label>
              <label className="full-row">
                Front image object key
                <input
                  value={dependentFrontImageObjectKey}
                  onChange={(event) => setDependentFrontImageObjectKey(event.target.value)}
                />
              </label>
            </div>
          </section>

          {myRequests.map((request) => (
            <RentalRequestCard
              key={request.id}
              request={request}
              contractDetail={request.contract ? contractDetails[request.contract.id] : undefined}
              contractPreview={request.contract ? contractPreviews[request.contract.id] : undefined}
              contractFiles={request.contract ? contractFiles[request.contract.id] : undefined}
              disabled={isLoading}
              side="tenant"
              onCancel={() => runAction(() => rentalRequestApi.cancelRentalRequest(request.id), 'Da huy yeu cau thue.')}
              onMarkPaid={() => {
                if (!request.deposit) return;
                return runAction(() => rentalRequestApi.markDepositPaid(request.deposit!.id), 'Da mark-paid khoan coc.');
              }}
              onSubmitOccupants={() => submitOccupants(request)}
              onTenantSign={() => {
                if (!request.contract) return;
                return runAction(
                  () => requestTenantOtpAndSign(request.contract!.id),
                  'Tenant da ky hop dong.'
                );
              }}
              onRevision={() => {
                if (!request.contract) return;
                return runAction(
                  () =>
                    contractApi.requestContractRevision(request.contract!.id, {
                      revisionType: 'ContractTerms',
                      reason: 'Tenant yeu cau sua noi dung hop dong.'
                    }),
                  'Da gui yeu cau sua dieu khoan hop dong.'
                );
              }}
              onOccupantRevision={() => {
                if (!request.contract) return;
                return runAction(
                  () =>
                    contractApi.requestContractRevision(request.contract!.id, {
                      revisionType: 'Occupants',
                      reason: 'Tenant yeu cau sua thong tin nguoi o.'
                    }),
                  'Da gui yeu cau sua thong tin nguoi o.'
                );
              }}
              onRejectContract={() => {
                if (!request.contract) return;
                return runAction(
                  () => contractApi.rejectContract(request.contract!.id, { reason: 'Tenant tu choi ky hop dong.' }),
                  'Tenant da tu choi hop dong.'
                );
              }}
              onViewContract={() => {
                if (!request.contract) return;
                return viewContract(request.contract.id);
              }}
              onViewPreview={() => {
                if (!request.contract) return;
                return viewContractPreview(request.contract.id);
              }}
              onGenerateContractFile={() => {
                if (!request.contract) return;
                return generateContractFile(request.contract.id);
              }}
              onLoadContractFiles={() => {
                if (!request.contract) return;
                return loadContractFiles(request.contract.id);
              }}
              onDownloadContractFile={(fileId) => {
                if (!request.contract) return;
                return downloadContractFile(request.contract.id, fileId);
              }}
            />
          ))}
        </section>

        <section className="rental-test-panel">
          <h2>Landlord flow</h2>
          <p>Dung tai khoan landlord de approve request, ky, yeu cau sua hoac reject contract.</p>

          {incomingRequests.map((request) => (
            <RentalRequestCard
              key={request.id}
              request={request}
              contractDetail={request.contract ? contractDetails[request.contract.id] : undefined}
              contractPreview={request.contract ? contractPreviews[request.contract.id] : undefined}
              contractFiles={request.contract ? contractFiles[request.contract.id] : undefined}
              disabled={isLoading}
              side="landlord"
              onApprove={() =>
                runAction(
                  () => rentalRequestApi.approveRentalRequest(request.id, { paymentDeadlineAt: addHoursIso(24) }),
                  'Landlord da approve va tao khoan coc.'
                )
              }
              onRejectRequest={() =>
                runAction(
                  () => rentalRequestApi.rejectRentalRequest(request.id, { rejectedReason: 'Landlord tu choi request test.' }),
                  'Landlord da reject rental request.'
                )
              }
              onLandlordSign={() => {
                if (!request.contract) return;
                return runAction(
                  () => requestLandlordOtpAndSign(request.contract!.id),
                  'Landlord da ky hop dong.'
                );
              }}
              onRevision={() => {
                if (!request.contract) return;
                return runAction(
                  () =>
                    contractApi.requestContractRevision(request.contract!.id, {
                      revisionType: 'Occupants',
                      reason: 'Landlord yeu cau tenant sua thong tin nguoi o.'
                    }),
                  'Landlord da yeu cau sua.'
                );
              }}
              onUpdateTerms={(payload) => {
                if (!request.contract) return;
                return runAction(
                  () => contractApi.updateContractTerms(request.contract!.id, payload),
                  'Landlord da cap nhat dieu khoan hop dong.'
                );
              }}
              onRejectContract={() => {
                if (!request.contract) return;
                return runAction(
                  () => contractApi.rejectContract(request.contract!.id, { reason: 'Landlord tu choi thong tin hop dong.' }),
                  'Landlord da reject contract.'
                );
              }}
              onViewContract={() => {
                if (!request.contract) return;
                return viewContract(request.contract.id);
              }}
              onViewPreview={() => {
                if (!request.contract) return;
                return viewContractPreview(request.contract.id);
              }}
              onGenerateContractFile={() => {
                if (!request.contract) return;
                return generateContractFile(request.contract.id);
              }}
              onLoadContractFiles={() => {
                if (!request.contract) return;
                return loadContractFiles(request.contract.id);
              }}
              onDownloadContractFile={(fileId) => {
                if (!request.contract) return;
                return downloadContractFile(request.contract.id, fileId);
              }}
            />
          ))}
        </section>
      </div>
    </div>
  );
}

interface RentalRequestCardProps {
  request: RentalRequestResponse;
  contractDetail?: ContractDetailResponse;
  contractPreview?: ContractPreviewResponse;
  contractFiles?: ContractFileResponse[];
  disabled: boolean;
  side: 'tenant' | 'landlord';
  onApprove?: () => void | Promise<void>;
  onRejectRequest?: () => void | Promise<void>;
  onCancel?: () => void | Promise<void>;
  onMarkPaid?: () => void | Promise<void>;
  onSubmitOccupants?: () => void | Promise<void>;
  onLandlordSign?: () => void | Promise<void>;
  onTenantSign?: () => void | Promise<void>;
  onRevision?: () => void | Promise<void>;
  onOccupantRevision?: () => void | Promise<void>;
  onUpdateTerms?: (payload: UpdateContractTermsRequest) => void | Promise<void>;
  onRejectContract?: () => void | Promise<void>;
  onViewContract?: () => void | Promise<void>;
  onViewPreview?: () => void | Promise<void>;
  onGenerateContractFile?: () => void | Promise<void>;
  onLoadContractFiles?: () => void | Promise<void>;
  onDownloadContractFile?: (fileId: string) => void | Promise<void>;
}

function RentalRequestCard({
  request,
  contractDetail,
  contractPreview,
  contractFiles,
  disabled,
  side,
  onApprove,
  onRejectRequest,
  onCancel,
  onMarkPaid,
  onSubmitOccupants,
  onLandlordSign,
  onTenantSign,
  onRevision,
  onOccupantRevision,
  onUpdateTerms,
  onRejectContract,
  onViewContract,
  onViewPreview,
  onGenerateContractFile,
  onLoadContractFiles,
  onDownloadContractFile
}: RentalRequestCardProps) {
  const contractStatus = request.contract?.status;
  const depositStatus = request.deposit?.status;
  const [termsStartDate, setTermsStartDate] = useState(contractDetail?.startDate ?? request.desiredStartDate);
  const [termsEndDate, setTermsEndDate] = useState(contractDetail?.endDate ?? request.expectedEndDate);
  const [termsPaymentDay, setTermsPaymentDay] = useState(contractDetail?.paymentDay ?? 5);

  useEffect(() => {
    if (!contractDetail) {
      return;
    }

    setTermsStartDate(contractDetail.startDate);
    setTermsEndDate(contractDetail.endDate);
    setTermsPaymentDay(contractDetail.paymentDay);
  }, [contractDetail]);

  return (
    <article className="rental-test-card">
      <h3>
        {request.roomingHouseName} - Phong {request.roomNumber}
      </h3>
      <div className="rental-test-meta">
        <span className="rental-test-status">Request: {request.status}</span>
        <span>RequestId: {request.id}</span>
        <span>Tenant: {request.tenantName}</span>
        <span>
          Thoi gian: {request.desiredStartDate} den {request.expectedEndDate}
        </span>
        <span>So nguoi request: {request.expectedOccupantCount}</span>
        <span>Gia thue snapshot: {formatMoney(request.monthlyRentSnapshot)}</span>
        <span>Coc snapshot: {formatMoney(request.depositAmountSnapshot)}</span>
        <span>Deposit: {depositStatus ?? '-'}</span>
        <span>Contract: {contractStatus ?? '-'}</span>
        {request.contract && <span>Contract files: {contractFiles?.length ?? 0}</span>}
        {request.deposit?.paymentDeadlineAt && <span>Han thanh toan coc: {request.deposit.paymentDeadlineAt}</span>}
        {request.deposit?.forfeitedAmount != null && <span>Coc mat: {formatMoney(request.deposit.forfeitedAmount)}</span>}
        {request.contract?.signatureDeadlineAt && <span>Han tenant ky: {request.contract.signatureDeadlineAt}</span>}
        {(request.rejectedReason || request.contract?.statusReason) && (
          <span>Ly do/trang thai: {request.contract?.statusReason ?? request.rejectedReason}</span>
        )}
      </div>

      {side === 'landlord' && contractStatus === 'TenantRevisionRequested' && (
        <div className="rental-test-terms-editor">
          <h4>Sua dieu khoan hop dong</h4>
          <div className="rental-test-form">
            <label>
              Ngay bat dau
              <input type="date" value={termsStartDate} onChange={(event) => setTermsStartDate(event.target.value)} />
            </label>
            <label>
              Ngay ket thuc
              <input type="date" value={termsEndDate} onChange={(event) => setTermsEndDate(event.target.value)} />
            </label>
            <label>
              Ngay thanh toan
              <input
                type="number"
                min={1}
                max={28}
                value={termsPaymentDay}
                onChange={(event) => setTermsPaymentDay(Number(event.target.value))}
              />
            </label>
          </div>
          <button
            className="rental-test-button primary"
            disabled={disabled}
            onClick={() =>
              onUpdateTerms?.({
                startDate: termsStartDate,
                endDate: termsEndDate,
                paymentDay: termsPaymentDay
              })
            }
          >
            Update terms
          </button>
        </div>
      )}

      <div className="rental-test-actions">
        {side === 'tenant' && request.status === 'Pending' && (
          <button className="rental-test-button danger" disabled={disabled} onClick={onCancel}>
            Cancel request
          </button>
        )}

        {side === 'tenant' && request.deposit?.status === 'PendingPayment' && (
          <button className="rental-test-button primary" disabled={disabled} onClick={onMarkPaid}>
            Mark deposit paid
          </button>
        )}

        {side === 'tenant' && contractStatus && ['WaitingTenantOccupants', 'LandlordRevisionRequested'].includes(contractStatus) && (
          <button className="rental-test-button primary" disabled={disabled} onClick={onSubmitOccupants}>
            Submit occupants
          </button>
        )}

        {side === 'tenant' && contractStatus === 'PendingTenantSignature' && (
          <>
            <button className="rental-test-button primary" disabled={disabled} onClick={onTenantSign}>
              Tenant sign
            </button>
            <button className="rental-test-button" disabled={disabled} onClick={onRevision}>
              Request terms revision
            </button>
            <button className="rental-test-button" disabled={disabled} onClick={onOccupantRevision}>
              Request occupants revision
            </button>
            <button className="rental-test-button danger" disabled={disabled} onClick={onRejectContract}>
              Reject contract
            </button>
          </>
        )}

        {side === 'landlord' && request.status === 'Pending' && (
          <>
            <button className="rental-test-button primary" disabled={disabled} onClick={onApprove}>
              Approve
            </button>
            <button className="rental-test-button danger" disabled={disabled} onClick={onRejectRequest}>
              Reject request
            </button>
          </>
        )}

        {side === 'landlord' && contractStatus && ['PendingLandlordSignature', 'TenantRevisionRequested'].includes(contractStatus) && (
          <>
            <button className="rental-test-button primary" disabled={disabled} onClick={onLandlordSign}>
              Landlord sign
            </button>
            {contractStatus === 'PendingLandlordSignature' && (
              <>
                <button className="rental-test-button" disabled={disabled} onClick={onRevision}>
                  Request revision
                </button>
                <button className="rental-test-button danger" disabled={disabled} onClick={onRejectContract}>
                  Reject contract
                </button>
              </>
            )}
          </>
        )}

        {request.contract && (
          <>
            <button className="rental-test-button" disabled={disabled} onClick={onViewPreview}>
              Preview contract
            </button>
            <button className="rental-test-button" disabled={disabled} onClick={onViewContract}>
              View contract detail
            </button>
            <button className="rental-test-button" disabled={disabled} onClick={onLoadContractFiles}>
              Load contract files
            </button>
          </>
        )}

        {request.contract && contractStatus === 'Active' && (
          <button className="rental-test-button primary" disabled={disabled} onClick={onGenerateContractFile}>
            Generate PDF
          </button>
        )}
      </div>

      {contractFiles && contractFiles.length > 0 && (
        <div className="rental-test-files">
          <h4>Contract files</h4>
          {contractFiles.map((file) => (
            <div key={file.id} className="rental-test-file-row">
              <span>{file.storageObjectKey}</span>
              <span>{file.createdAt}</span>
              <button
                className="rental-test-button"
                disabled={disabled}
                onClick={() => onDownloadContractFile?.(file.id)}
              >
                Download PDF
              </button>
            </div>
          ))}
        </div>
      )}

      {contractPreview && (
        <pre className="rental-test-preview">
          {contractPreview.renderedContent}
        </pre>
      )}

      {contractDetail && (
        <pre className="rental-test-json">
          {JSON.stringify(contractDetail, null, 2)}
        </pre>
      )}
    </article>
  );
}


namespace SmartRentalPlatform.Domain.Enums.Notifications;

public enum NotificationType
{
    NewViewingAppointment,
    ViewingAppointmentConfirmed,
    ViewingAppointmentRejected,
    ViewingAppointmentCancelled,
    ViewingAppointmentCompleted,
    NewRentalRequest,
    RentalRequestApproved,
    RentalRequestRejected,
    NewChatMessage,
    RoomingHouseReviewNeedsAdminReview,
    RoomingHouseReviewRejected,
    RoomingHouseReviewReplied,
    ContractAwaitingLandlordSignature,
    ContractAwaitingTenantSignature,
    ContractActivated,
    ContractExpired,
    ContractRevisionRequested,
    ContractAppendixAwaitingSignature,
    ContractAppendixActivated,
    ContractAppendixRevisionRequested,
    ContractAppendixRejected,
}

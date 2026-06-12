namespace SmartRentalPlatform.Contracts.ViewingAppointments.Requests
{
    public class ConfirmViewingAppointmentRequest
    {
        public bool ConfirmDespiteConflict { get; set; }
        public string? LandlordNote { get; set; }
    }
}

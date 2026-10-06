using Microsoft.AspNetCore.SignalR;

namespace Business.Hubs
{
    public class ClinicHub : Hub
    {
        public async Task JoinDoctorGroup(string doctorId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"doctor_{doctorId}");
        }

        public async Task JoinAdminGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admin_group");
        }
    }
}
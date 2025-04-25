
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace ticketicos.Hubs
{
    public class ChatHub : Hub
    {
        // Enviar un mensaje a un ticket específico
        public async Task SendMessageToTicket(int ticketId, string message, string sender, bool isAdmin)
        {
            await Clients.Group($"Ticket_{ticketId}").SendAsync("ReceiveMessage", ticketId, message, sender, isAdmin);
        }

        // Unir a un usuario o administrador a un grupo de ticket
        public async Task JoinTicket(int ticketId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");
        }

        // Salir de un grupo de ticket
        public async Task LeaveTicket(int ticketId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");
        }
    }
}
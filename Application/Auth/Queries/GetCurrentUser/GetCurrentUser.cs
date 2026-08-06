using Application.Auth.Models;
using Application.Common.Interfaces;
using MediatR;

namespace Application.Auth.Queries.GetCurrentUser
{
    /// <summary>
    /// Lấy thông tin user đang đăng nhập từ claims trong token — KHÔNG truy DB,
    /// nên gọi bao nhiêu lần cũng rẻ. Frontend dùng khi F5 để dựng lại UI theo quyền.
    /// </summary>
    public record GetCurrentUserQuery : IRequest<CurrentUserResponse>;

    public class GetCurrentUserQueryHandler(ICurrentUser currentUser)
        : IRequestHandler<GetCurrentUserQuery, CurrentUserResponse>
    {
        public Task<CurrentUserResponse> Handle(GetCurrentUserQuery request, CancellationToken ct)
        {
            // Endpoint đã có [Authorize] nên tới đây chắc chắn có Id.
            var response = new CurrentUserResponse
            {
                Id = currentUser.Id!,
                Email = currentUser.Email,
                Roles = currentUser.Roles,
                Permissions = currentUser.Permissions
            };

            return Task.FromResult(response);
        }
    }
}

using Application.Common.Interfaces;
using Application.Users.Models;
using MediatR;
namespace Application.Users.Queries
{
    public record GetUserByIdQuery(string UserId) : IRequest<UserDto>;

    public class GetUserByIdQueryHandler(IUserAdminService users)
        : IRequestHandler<GetUserByIdQuery, UserDto>
    {
        public Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken ct)
            => users.GetUserByIdAsync(request.UserId, ct);
    }
}

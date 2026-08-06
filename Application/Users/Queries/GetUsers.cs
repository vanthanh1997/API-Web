using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Users.Models;
using FluentValidation;
using MediatR;

namespace Application.Users.Queries
{
    /// <summary>Danh sách user cho trang quản trị, lọc theo từ khoá và role.</summary>
    public record GetUsersQuery(
        string? Keyword = null,
        string? Role = null,
        int Page = 1,
        int PageSize = 20) : IRequest<PagedList<UserDto>>;

    public class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
    {
        public GetUsersQueryValidator()
        {
            RuleFor(x => x.Page).GreaterThan(0);

            // Chặn trên để client không xin 1 triệu bản ghi làm sập DB.
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        }
    }

    public class GetUsersQueryHandler(IUserAdminService users)
        : IRequestHandler<GetUsersQuery, PagedList<UserDto>>
    {
        public Task<PagedList<UserDto>> Handle(GetUsersQuery request, CancellationToken ct)
            => users.GetUsersAsync(request.Keyword, request.Role, request.Page, request.PageSize, ct);
    }
}

using RoomBooking.Api.Contracts;
using RoomBooking.Services.Models;

namespace RoomBooking.Api.Mappings;

public static class ResponseMappings
{
    public static RoomResponse ToResponse(this RoomModel model)
    {
        return new RoomResponse(model.Id, model.Name, model.Capacity);
    }

    public static ReservationResponse ToResponse(this ReservationModel model)
    {
        return new ReservationResponse(
            model.Id,
            model.RoomId,
            model.StartUtc.ToUniversalTime(),
            model.EndUtc.ToUniversalTime(),
            model.Title,
            model.CreatedAtUtc.ToUniversalTime());
    }

    public static PagedResponse<TResponse> ToResponse<TModel, TResponse>(
        this PagedResult<TModel> model,
        Func<TModel, TResponse> map)
    {
        var items = model.Items.Select(map).ToArray();
        var hasMore = model.Offset + items.Length < model.TotalCount;

        return new PagedResponse<TResponse>(items, model.Limit, model.Offset, model.TotalCount, hasMore);
    }
}

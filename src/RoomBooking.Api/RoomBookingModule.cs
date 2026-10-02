using Autofac;
using RoomBooking.Services.Services;
using RoomBooking.Services.Time;

namespace RoomBooking.Api;

public sealed class RoomBookingModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RoomBookingService>()
            .As<IRoomBookingService>()
            .InstancePerLifetimeScope();

        builder.RegisterType<ReservationVersionTokenService>()
            .As<IReservationVersionTokenService>()
            .InstancePerLifetimeScope();

        builder.RegisterType<SystemClock>()
            .As<IClock>()
            .SingleInstance();
    }
}

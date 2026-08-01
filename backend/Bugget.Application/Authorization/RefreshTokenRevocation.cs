using System;

namespace Bugget.Application.Authorization;

/// <summary>
/// Одна политика времени для refresh-токена: допуск на расхождение часов и производная
/// от него граница ревокации.
/// </summary>
/// <remarks>
/// Lifetime-валидатор принимает токен включительно до <c>exp + ClockSkew</c>. Значит и
/// ревокация обязана жить ровно до этого момента: если она заканчивается на <c>exp</c>,
/// то между <c>exp</c> и <c>exp + ClockSkew</c> уже отозванный refresh снова проходит
/// ротацию и выпускает вторую пару токенов.
/// </remarks>
public static class RefreshTokenRevocation
{
    /// <summary>
    /// Допуск на расхождение часов при проверке lifetime refresh-токена.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Момент, до которого включительно токен с указанным <paramref name="expires"/> ещё
    /// принимается lifetime-валидатором, а значит должен оставаться отозванным.
    /// </summary>
    public static DateTimeOffset RevokedUntil(DateTimeOffset expires) => expires + ClockSkew;
}

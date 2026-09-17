using System;

namespace Bugget.Application.Authorization;

/// <summary>Одна политика времени для refresh-токена: допуск на расхождение часов и граница ревокации.</summary>
/// <remarks>
/// Lifetime-валидатор принимает токен до <c>exp + ClockSkew</c> включительно, поэтому ревокация живёт до того же
/// момента: иначе между <c>exp</c> и <c>exp + ClockSkew</c> отозванный refresh снова пройдёт ротацию.
/// </remarks>
public static class RefreshTokenRevocation
{
    /// <summary>Допуск на расхождение часов при проверке lifetime refresh-токена.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(10);

    /// <summary>Момент, до которого включительно токен с <paramref name="expires"/> должен оставаться отозванным.</summary>
    public static DateTimeOffset RevokedUntil(DateTimeOffset expires) => expires + ClockSkew;
}

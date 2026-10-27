/// <summary>Freezes mutable Camp region facts while its final snapshot transfers to a run.</summary>
public interface ICampDepartureState
{
    bool DeparturePending { get; }
    void BeginDeparture();
    void CancelDeparture();
}

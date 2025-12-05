namespace CinemaSeatBooking

module ApiIntegration =

    open System
    open System.Threading
    open CinemaSeatBooking

    // Thread-safe lock for API operations
    let private apiLock = obj()

    // ������ �� ��� �����
    let validateRequest (request: ReserveRequest) =
        if request.SeatPositions.IsEmpty then
            Error "Seat positions cannot be empty"
        elif request.SeatPositions |> List.exists (fun (r, c) -> r <= 0 || c <= 0) then
            Error "Invalid seat positions: row and column must be positive"
        else
            Ok request

    // API: ������ ��� ���� ������� - Thread-safe
    let getSeatsApi (state: CinemaState) =
        lock apiLock (fun () ->
            {
                Success = true
                Data = Some state.Seats
                Error = None
            }
        )

    // API: حجز كراسي - Thread-safe
    let postReserveSeatsApi (gridId: string) (request: ReserveRequest) (state: CinemaState) =
        lock apiLock (fun () ->
            match validateRequest request with
            | Error msg -> 
                {
                    Success = false
                    Data = None
                    Error = Some msg
                }
            | Ok validRequest ->
                match BookingLogic.createTicket gridId validRequest.SeatPositions state.Seats with
                | Error badSeats ->
                    {
                        Success = false
                        Data = None
                        Error = Some $"Seats unavailable: {badSeats}"
                    }
                | Ok (ticket, updatedSeats) ->
                    let newState = { 
                        Seats = updatedSeats
                        Tickets = ticket :: state.Tickets 
                    }
                    {
                        Success = true
                        Data = Some (ticket, newState)
                        Error = None
                    }
        )

    // API: ������ ��� ����� ����� - Thread-safe
    let getTicketApi (ticketId: Guid) (state: CinemaState) =
        lock apiLock (fun () ->
            match state.Tickets |> List.tryFind (fun t -> t.Id = ticketId) with
            | Some ticket ->
                {
                    Success = true
                    Data = Some ticket
                    Error = None
                }
            | None ->
                {
                    Success = false
                    Data = None
                    Error = Some "Ticket not found"
                }
        )

    // API: ������ ��� ���� ������� - Thread-safe
    let getAllTicketsApi (state: CinemaState) =
        lock apiLock (fun () ->
            {
                Success = true
                Data = Some state.Tickets
                Error = None
            }
        )

    // API: ����� ��� - Thread-safe
    let deleteTicketApi (ticketId: Guid) (state: CinemaState) =
        lock apiLock (fun () ->
            match BookingLogic.cancelReservation ticketId state with
            | Ok newState ->
                {
                    Success = true
                    Data = Some newState
                    Error = None
                }
            | Error msg ->
                {
                    Success = false
                    Data = None
                    Error = Some msg
                }
        )

    // API: �������� ������� - Thread-safe
    let getStatsApi (state: CinemaState) =
        lock apiLock (fun () ->
            let availableCount = state.Seats |> List.filter (fun s -> s.Status = Available) |> List.length
            let reservedCount = state.Seats |> List.filter (fun s -> s.Status = Reserved) |> List.length
            let totalSeats = state.Seats.Length
            let totalTickets = state.Tickets.Length
        
            let stats = {|
                TotalSeats = totalSeats
                AvailableSeats = availableCount
                ReservedSeats = reservedCount
                TotalTickets = totalTickets
                OccupancyRate = if totalSeats > 0 then (float reservedCount / float totalSeats * 100.0) else 0.0
            |}
        
            {
                Success = true
                Data = Some stats
                Error = None
            }
        )

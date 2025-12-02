namespace CinemaSeatBooking

module ApiIntegration =

    open CinemaSeatBooking

    // ÇáÊÍÞÞ ãä ÕÍÉ ÇáØáÈ
    let validateRequest (request: ReserveRequest) =
        if request.SeatPositions.IsEmpty then
            Error "Seat positions cannot be empty"
        elif request.SeatPositions |> List.exists (fun (r, c) -> r <= 0 || c <= 0) then
            Error "Invalid seat positions: row and column must be positive"
        else
            Ok request

    // API: ÇáÍÕæá Úáì ÌãíÚ ÇáßÑÇÓí
    let getSeatsApi (state: CinemaState) =
        {
            Success = true
            Data = Some state.Seats
            Error = None
        }

    // API: ÍÌÒ ÇáßÑÇÓí
    let postReserveSeatsApi (request: ReserveRequest) (state: CinemaState) =
        match validateRequest request with
        | Error msg -> 
            {
                Success = false
                Data = None
                Error = Some msg
            }
        | Ok validRequest ->
            match BookingLogic.createTicket validRequest.SeatPositions state.Seats with
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

    // API: ÇáÍÕæá Úáì ÊÐßÑÉ ãÚíäÉ
    let getTicketApi (ticketId: Guid) (state: CinemaState) =
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

    // API: ÇáÍÕæá Úáì ÌãíÚ ÇáÊÐÇßÑ
    let getAllTicketsApi (state: CinemaState) =
        {
            Success = true
            Data = Some state.Tickets
            Error = None
        }

    // API: ÅáÛÇÁ ÍÌÒ
    let deleteTicketApi (ticketId: Guid) (state: CinemaState) =
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

    // API: ÅÍÕÇÆíÇÊ ÇáÓíäãÇ
    let getStatsApi (state: CinemaState) =
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

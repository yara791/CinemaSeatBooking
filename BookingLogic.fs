namespace CinemaSeatBooking

module BookingLogic =

    open System
    open CinemaSeatBooking
    open SeatManagement

    // ÇáÝÇäßÔä ÈÊÇÚÉ ÇáÍÌÒ
    let reserveSeats seatPositions seats =
        let unavailable =
            seatPositions
            |> List.choose (fun (r, c) ->
                match selectSeat r c seats with
                | None -> Some (r, c)
                | Some s when s.Status = SeatStatus.Reserved -> Some (r, c)
                | _ -> None)

        if unavailable.Length > 0 then
            Error unavailable
        else
            let updated =
                seatPositions
                |> List.fold (fun acc (r, c) ->
                    acc |> List.map (fun s ->
                        if s.Row = r && s.Col = c then { s with Status = SeatStatus.Reserved }
                        else s)) seats
            Ok updated

    // ÊæáíÏ GUID
    let generateTicketId() = Guid.NewGuid()

    // ÅäÔÇÁ ÇáÊíßÊ
    let createTicket seatPositions seats =
        match reserveSeats seatPositions seats with
        | Error badSeats -> Error badSeats
        | Ok updated ->
            let booked =
                seatPositions
                |> List.choose (fun (r, c) -> selectSeat r c updated)
            let ticket = {
                Id = generateTicketId()
                Seats = booked
                CreatedAt = DateTime.UtcNow
            }
            Ok (ticket, updated)

    // ÅáÛÇÁ ÇáÍÌÒ
    let cancelReservation ticketId (state: CinemaState) =
        match state.Tickets |> List.tryFind (fun t -> t.Id = ticketId) with
        | None -> Error "Ticket not found"
        | Some ticket ->
            let seatPositions = ticket.Seats |> List.map (fun s -> (s.Row, s.Col))
            let updatedSeats =
                state.Seats
                |> List.map (fun s ->
                    if seatPositions |> List.contains (s.Row, s.Col) then
                        { s with Status = SeatStatus.Available }
                    else s)
            let updatedTickets = state.Tickets |> List.filter (fun t -> t.Id <> ticketId)
            Ok { Seats = updatedSeats; Tickets = updatedTickets }

    // ÚÑÖ ÊÝÇÕíá ÇáÊíßÊ
    let displayTicket (ticket: Ticket) =
        printfn "\n--- Ticket Details ---"
        printfn "ID: %A" ticket.Id
        printfn "Created: %s" (ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))
        printfn "Seats:"
        ticket.Seats |> List.iter (fun s -> printfn "  - Row %d, Col %d" s.Row s.Col)
        printfn "Total seats: %d" ticket.Seats.Length
        printfn "---------------------\n"

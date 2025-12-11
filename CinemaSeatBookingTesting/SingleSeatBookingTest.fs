namespace CinemaSeatBookingTesting

module SingleSeatBookingTest =
    open Xunit
    open CinemaSeatBooking
    open CinemaSeatBooking.SeatManagement 
    open CinemaSeatBooking.BookingLogic

    type SingleSeatBookingTest() =
        [<Fact>]
        member _.``Booking one seat changes its status to Reserved`` () =
            let seats = initializeSeatLayout 4 4
            match reserveSeats [ (1,1) ] seats with
            | Ok updatedSeats ->
                let bookedSeat = updatedSeats |> List.find (fun s -> s.Row = 1 && s.Col = 1)
                Assert.Equal(SeatStatus.Reserved, bookedSeat.Status)

                let otherSeats = updatedSeats |> List.filter (fun s -> not (s.Row = 1 && s.Col = 1))
                Assert.All(otherSeats, fun s -> Assert.Equal(SeatStatus.Available, s.Status))

            | Error _ ->
                Assert.True(false, "Seat (1,1) should have been available for booking")

namespace CinemaSeatBookingTesting

module DuplicateBookingTest =
    open Xunit
    open CinemaSeatBooking
    open CinemaSeatBooking.SeatManagement 
    open CinemaSeatBooking.BookingLogic

    type DuplicateBookingTest() =
        [<Fact>]
        member _.``Double booking same seat one after another`` () =
            let seats = initializeSeatLayout 4 4
            
            // First booking of seat (2,2)
            match reserveSeats [ (2,2) ] seats with
            | Ok updatedSeats ->
                let bookedSeat = updatedSeats |> List.find (fun s -> s.Row = 2 && s.Col = 2)
                Assert.Equal(SeatStatus.Reserved, bookedSeat.Status)
                
                // Attempt to book the same seat again
                match reserveSeats [ (2,2) ] updatedSeats with
                | Ok _ ->
                    Assert.True(false, "Second booking of same seat should have failed")
                | Error unavailableSeats ->
                    // Should contain the already reserved seat (2,2)
                    Assert.Contains((2, 2), unavailableSeats)
            | Error _ ->
                Assert.True(false, "First booking should have succeeded")

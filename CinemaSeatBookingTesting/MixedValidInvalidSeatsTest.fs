namespace CinemaSeatBookingTesting

module MixedValidInvalidSeatsTest =
    open Xunit
    open CinemaSeatBooking
    open CinemaSeatBooking.SeatManagement 
    open CinemaSeatBooking.BookingLogic

    type MixedValidInvalidSeatsTest() =
        [<Fact>]
        member _.``Mixed valid and invalid seats`` () =
            let seats = initializeSeatLayout 4 4
            
            // Try to book mix of valid seats and invalid seats (out of range)
            match reserveSeats [ (1,1); (10,10); (2,2) ] seats with
            | Ok _ ->
                Assert.True(false, "Booking should have failed due to invalid seat (10,10)")
            | Error unavailableSeats ->
                // Should contain the invalid seat (10,10)
                Assert.Contains((10, 10), unavailableSeats)

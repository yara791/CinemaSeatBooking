namespace CinemaSeatBookingTesting

module InitTest =
    open Xunit
    open CinemaSeatBooking
    open CinemaSeatBooking.SeatManagement 


    type InitializationTest() =
        [<Fact>]
        member _.``Initialization creates all seats as available in 4x4 grid`` () =
            let seats = initializeSeatLayout 4 4
            Assert.Equal(16, seats.Length)
            Assert.All(seats, fun s -> Assert.Equal(SeatStatus.Available, s.Status))


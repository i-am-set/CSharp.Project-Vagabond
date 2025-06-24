using ProjectVagabond;
using System.Numerics;

public class example_Campfire : Entity
{
    private int _fuel; // in minutes

    public example_Campfire(Vector2 position, int initialFuel) 
        : base("Campfire", EntityType.Effect, position)
    {
        _fuel = initialFuel;
    }

    public override void Update(int minutesPassed, GameState gameState)
    {
        _fuel -= minutesPassed;
        if (_fuel <= 0)
        {
            // The fire has burned out.
            // You could remove it from the world or change its state.
            System.Diagnostics.Debug.WriteLine("Campfire burned out.");
        }
    }
}
﻿using System;

namespace ProjectVagabond
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new Core())
                game.Run();
        }
    }
}
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;

namespace osu.Game.Configuration
{
    /// <summary>
    /// torii: cuanta CPU usa el recalculo de star rating de fondo (cuando cambia la version del
    /// algoritmo de dificultad y hay que regenerar la metadata de todos los mapas). el usuario lo
    /// elige en el popup de arranque. el default es el modo gentil de lazer asi, si el popup se cierra
    /// o falla, no se come la maquina.
    /// </summary>
    public enum ToriiDifficultyRecalcMode
    {
        [Description("Gentle (lazer default, single thread)")]
        LazerDefault = 0,

        [Description("Half the cores (faster)")]
        HalfCores = 1,

        [Description("i paid for the whole cpu")]
        MaxCores = 2,
    }

    public static class ToriiDifficultyRecalc
    {
        /// <summary>
        /// cantidad de threads para cada modo. el max deja 1 core libre asi la maquina no se cuelga.
        /// </summary>
        public static int ParallelismFor(ToriiDifficultyRecalcMode mode)
        {
            int cores = Environment.ProcessorCount;

            switch (mode)
            {
                case ToriiDifficultyRecalcMode.HalfCores:
                    return Math.Max(1, cores / 2);

                case ToriiDifficultyRecalcMode.MaxCores:
                    return Math.Max(1, cores <= 4 ? cores - 1 : (cores <= 8 ? cores - 2 : cores - 4));

                default:
                    return Math.Max(1, Math.Min(4, cores <= 4 ? 1 : cores / 4));
            }
        }

        /// <summary>
        /// ETA muy aproximado en segundos para recalcular <paramref name="count"/> mapas con
        /// <paramref name="threads"/> threads. Con batches de base de datos y cache bypass,
        /// la paralelización escala de manera eficiente entre núcleos.
        /// </summary>
        public static int EstimateSeconds(int count, int threads)
        {
            double rate = Math.Max(5.0, 8.0 + (Math.Max(1, threads) - 1) * 3.5);
            return (int)Math.Ceiling(count / rate);
        }

        /// <summary>
        /// Formatea un ETA en segundos a algo legible: "45s", "12 min", "2 h 5 min". Nunca muestra
        /// cientos de segundos ni de minutos sueltos.
        /// </summary>
        public static string FormatEta(int seconds)
        {
            if (seconds < 60)
                return $"{seconds}s";

            int minutes = (int)Math.Round(seconds / 60.0);

            if (minutes < 60)
                return $"{minutes} min";

            int hours = minutes / 60;
            int remainingMinutes = minutes % 60;

            return remainingMinutes == 0 ? $"{hours} h" : $"{hours} h {remainingMinutes} min";
        }
    }
}

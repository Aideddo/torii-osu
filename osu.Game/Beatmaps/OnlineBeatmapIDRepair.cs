// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// torii: rescata mapas que quedaron con el id online borrado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El lookup de metadata trata CUALQUIER fallo de request como "este mapa no existe
    /// online" (ver <see cref="APIBeatmapMetadataSource"/>: un request fallado devuelve
    /// true con metadata null) y ahi <see cref="BeatmapUpdaterMetadataLookup"/> llama a
    /// ResetOnlineInfo() y deja OnlineID en -1. Un timeout o un mirror caido un segundo
    /// alcanza para romperlo, y no distingue eso de un 404 de verdad.
    /// </para>
    /// <para>
    /// Peor: no se reintenta nunca. El barrido de arranque busca beatmaps con OnlineID &gt; 0,
    /// asi que saltea justo las filas que rompio. El mapa se sigue viendo perfecto (titulo,
    /// dificultad y SR salen del .osu, y el SET conserva su id) pero esa dificultad no
    /// submitea nunca mas, en silencio.
    /// </para>
    /// <para>
    /// Asi que antes de dar el score por perdido probamos resolverlo una vez mas por
    /// checksum. Si sale, arreglamos la instancia que esta usando el gameplay y ademas lo
    /// persistimos, asi la proxima vez ni hace falta.
    /// </para>
    /// </remarks>
    public static class OnlineBeatmapIDRepair
    {
        /// <summary>
        /// Intenta resolver el id online de <paramref name="beatmapInfo"/> si esta sin resolver.
        /// Devuelve true si el beatmap quedo con un id usable (ya lo tenia, o lo reparamos).
        /// </summary>
        /// <remarks>
        /// Bloquea, igual que el lookup de metadata original. Solo se llama en el camino del
        /// token, que ya es bloqueante, y unicamente cuando el id falta: en el camino sano
        /// sale en la primera linea y no cuesta nada.
        /// </remarks>
        public static bool TryRepair(BeatmapInfo beatmapInfo, IAPIProvider? api, RealmAccess? realm)
        {
            if (beatmapInfo.OnlineID > 0)
                return true;

            // sin conexion no hay nada que hacer, y un mapa editado localmente no deberia
            // submitear igual.
            if (api?.State.Value != APIState.Online)
                return false;

            if (string.IsNullOrEmpty(beatmapInfo.MD5Hash))
                return false;

            if (beatmapInfo.Status == BeatmapOnlineStatus.LocallyModified)
                return false;

            try
            {
                var req = new GetBeatmapRequest(md5Hash: beatmapInfo.MD5Hash, filename: beatmapInfo.Path);
                api.Perform(req);

                if (req.CompletionState != APIRequestCompletionState.Completed)
                    return false;

                var res = req.Response;

                if (res == null || res.OnlineID <= 0)
                    return false;

                // el lookup puede matchear por filename cuando el md5 no coincide, y eso nos
                // devolveria OTRO mapa. Submitear un score sobre el id equivocado es mucho
                // peor que no submitearlo, asi que exigimos que el checksum coincida.
                if (!string.Equals(res.MD5Hash, beatmapInfo.MD5Hash, StringComparison.OrdinalIgnoreCase))
                    return false;

                applyTo(beatmapInfo, res);
                persist(beatmapInfo, res, realm);

                Logger.Log($"Recovered online ID for {beatmapInfo} ({res.OnlineID}) — score submission enabled.");
                return true;
            }
            catch (Exception e)
            {
                Logger.Log($"Online ID repair failed for {beatmapInfo}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// El beatmap que usa el gameplay es una copia detached del de realm, y es la MISMA
        /// referencia que termina en Score.ScoreInfo.BeatmapInfo, asi que hay que arreglar
        /// esta instancia o el submit posterior sigue viendo -1.
        /// </summary>
        private static void applyTo(BeatmapInfo beatmapInfo, APIBeatmap res)
        {
            if (beatmapInfo.IsManaged)
                return;

            beatmapInfo.OnlineID = res.OnlineID;
            beatmapInfo.OnlineMD5Hash = res.MD5Hash;
            beatmapInfo.LastOnlineUpdate = res.LastUpdated;
            beatmapInfo.Status = res.Status;

            if (beatmapInfo.BeatmapSet != null && res.BeatmapSet?.OnlineID > 0)
                beatmapInfo.BeatmapSet.OnlineID = res.BeatmapSet.OnlineID;
        }

        /// <summary>
        /// Y lo guardamos, asi el arreglo sobrevive a cerrar el juego en vez de tener que
        /// re-resolverlo en cada play.
        /// </summary>
        private static void persist(BeatmapInfo beatmapInfo, APIBeatmap res, RealmAccess? realm)
        {
            if (realm == null)
                return;

            try
            {
                realm.Write(r =>
                {
                    var managed = r.Find<BeatmapInfo>(beatmapInfo.ID);

                    if (managed == null || managed.OnlineID > 0)
                        return;

                    managed.OnlineID = res.OnlineID;
                    managed.OnlineMD5Hash = res.MD5Hash;
                    managed.LastOnlineUpdate = res.LastUpdated;
                    managed.Status = res.Status;

                    if (managed.BeatmapSet != null && res.BeatmapSet?.OnlineID > 0)
                        managed.BeatmapSet.OnlineID = res.BeatmapSet.OnlineID;
                });
            }
            catch (Exception e)
            {
                // que no se persista no es fatal: el play en curso ya quedo arreglado arriba.
                Logger.Log($"Could not persist recovered online ID for {beatmapInfo}: {e.Message}");
            }
        }
    }
}

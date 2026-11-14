// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;

namespace osu.Game.Database
{
    public class LegacyScoreExporter : LegacyExporter<ScoreInfo>
    {
        private readonly ReplayExtrasStore extras;

        public LegacyScoreExporter(Storage storage)
            : base(storage)
        {
            extras = new ReplayExtrasStore(storage);
        }

        /// <summary>
        /// Torii: what was saved along with the replay (e.g. head and controller tracking of a play in VR) goes next to it, under the
        /// same name. Importing the replay from there takes it along again.
        /// </summary>
        protected override void OnExported(ScoreInfo model, string filename)
        {
            if (!extras.Exists(model.Hash))
                return;

            using (var stream = ExportStorage.CreateFileSafely(Path.ChangeExtension(filename, ReplayExtrasStore.EXTENSION)))
                extras.CopyTo(model.Hash, stream);
        }

        protected override string GetFilename(ScoreInfo score)
        {
            string scoreString = score.GetDisplayString();
            string filename = $"{scoreString} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm})";

            return filename;
        }

        protected override string FileExtension => @".osr";

        public override void ExportToStream(ScoreInfo model, Stream outputStream, ProgressNotification? notification, CancellationToken cancellationToken = default)
        {
            var file = model.Files.SingleOrDefault();
            if (file == null)
                return;

            using (var inputStream = UserFileStorage.GetStream(file.File.GetStoragePath()))
                inputStream.CopyTo(outputStream);
        }
    }
}

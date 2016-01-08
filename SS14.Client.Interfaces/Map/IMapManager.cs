﻿using Lidgren.Network;
using System.Collections.Generic;
using System.Drawing;
using SS14.Shared;
using SS14.Shared.Maths;
using SFML.System;
using SFML.Graphics;

namespace SS14.Client.Interfaces.Map
{
    public delegate void TileChangedEventHandler(TileRef tileRef, Tile oldTile);

    public interface IMapManager
    {
        event TileChangedEventHandler TileChanged;

        void HandleNetworkMessage(NetIncomingMessage message);

        int TileSize { get; }

        IEnumerable<TileRef> GetTilesIntersecting(FloatRect area, bool ignoreSpace);
        IEnumerable<TileRef> GetGasTilesIntersecting(FloatRect area);
        IEnumerable<TileRef> GetWallsIntersecting(FloatRect area);
        IEnumerable<TileRef> GetAllTiles();

        TileRef GetTileRef(Vector2f pos);
        TileRef GetTileRef(int x, int y);
        ITileCollection Tiles { get; }
    }
}
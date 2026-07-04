using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace DTXMania
{
	internal static class CFavoritesManager
	{
		public const string FavoriteBoxTitle = "< FAVORITES >";
		private const string FavoriteEntryPrefix = "#SONG";

		public static string FavoritesFilePath
		{
			get
			{
				return Path.Combine( CDTXMania.strEXEのあるフォルダ, "Favorites.def" );
			}
		}

		public static bool bIsFavorite( CSongListNode song )
		{
			string key = tGetFavoriteKey( song );
			if( string.IsNullOrEmpty( key ) )
				return false;

			return tLoadFavoriteKeys().Contains( tNormalizeKey( key ) );
		}

		public static bool bToggleFavorite( CSongListNode song )
		{
			string key = tGetFavoriteKey( song );
			if( string.IsNullOrEmpty( key ) )
				return false;

			HashSet<string> favoriteKeys = tLoadFavoriteKeys();
			string normalizedKey = tNormalizeKey( key );
			bool added;
			if( favoriteKeys.Contains( normalizedKey ) )
			{
				favoriteKeys.Remove( normalizedKey );
				added = false;
			}
			else
			{
				favoriteKeys.Add( normalizedKey );
				added = true;
			}

			tSaveFavoriteKeys( favoriteKeys );
			return added;
		}

		public static void tRebuildFavoritesBox( CSongManager songManager )
		{
			if( songManager == null || songManager.listSongRoot == null )
				return;

			tRemoveExistingFavoritesBox( songManager.listSongRoot );

			HashSet<string> favoriteKeys = tLoadFavoriteKeys();
			CSongListNode favoriteBox = tCreateFavoriteBox();
			tCollectFavoriteSongs( songManager.listSongRoot, favoriteKeys, favoriteBox );
			tAddBackBox( favoriteBox );

			int randomIndex = songManager.listSongRoot.FindIndex( n => n.eNodeType == CSongListNode.ENodeType.RANDOM );
			if( randomIndex >= 0 )
				songManager.listSongRoot.Insert( randomIndex, favoriteBox );
			else
				songManager.listSongRoot.Add( favoriteBox );
		}

		public static string tGetFavoriteKey( CSongListNode song )
		{
			if( song == null || song.eNodeType != CSongListNode.ENodeType.SCORE )
				return "";

			if( !string.IsNullOrEmpty( song.pathSetDefの絶対パス ) )
			{
				string relativeSetDefPath = tMakeRelativeToDtxPath( song.pathSetDefの絶対パス );
				if( string.IsNullOrEmpty( relativeSetDefPath ) )
					return "";

				return string.Format( CultureInfo.InvariantCulture, "setdef:{0}#block={1}", relativeSetDefPath, song.SetDefのブロック番号 );
			}

			CScore score = tGetFirstAvailableScore( song );
			if( score == null || string.IsNullOrEmpty( score.FileInformation.AbsoluteFilePath ) )
				return "";

			string relativeScorePath = tMakeRelativeToDtxPath( score.FileInformation.AbsoluteFilePath );
			if( string.IsNullOrEmpty( relativeScorePath ) )
				return "";

			return "score:" + relativeScorePath;
		}

		private static void tCollectFavoriteSongs( List<CSongListNode> sourceList, HashSet<string> favoriteKeys, CSongListNode favoriteBox )
		{
			if( sourceList == null )
				return;

			foreach( CSongListNode song in sourceList )
			{
				if( song == null )
					continue;

				if( song.eNodeType == CSongListNode.ENodeType.BOX )
				{
					if( song.strタイトル == FavoriteBoxTitle )
						continue;

					tCollectFavoriteSongs( song.list子リスト, favoriteKeys, favoriteBox );
				}
				else if( song.eNodeType == CSongListNode.ENodeType.SCORE )
				{
					string key = tGetFavoriteKey( song );
					if( favoriteKeys.Contains( tNormalizeKey( key ) ) )
					{
						CSongListNode favoriteSong = song.ShallowCopyOfSelf();
						favoriteSong.r親ノード = favoriteBox;
						favoriteSong.list子リスト = null;
						favoriteSong.strBreadcrumbs = favoriteBox.strBreadcrumbs + " > " + favoriteSong.strタイトル;
						favoriteBox.list子リスト.Add( favoriteSong );
					}
				}
			}
		}

		private static CSongListNode tCreateFavoriteBox()
		{
			CSongListNode favoriteBox = new CSongListNode();
			favoriteBox.eNodeType = CSongListNode.ENodeType.BOX;
			favoriteBox.bBoxDefで作成されたBOXである = true;
			favoriteBox.strタイトル = FavoriteBoxTitle;
			favoriteBox.strBreadcrumbs = FavoriteBoxTitle;
			favoriteBox.nスコア数 = 1;
			favoriteBox.list子リスト = new List<CSongListNode>();
			favoriteBox.col文字色 = ColorTranslator.FromHtml( "#FFD966" );
			favoriteBox.arScore[ 0 ] = new CScore();
			favoriteBox.arScore[ 0 ].SongInformation.Title = FavoriteBoxTitle;
			favoriteBox.arScore[ 0 ].SongInformation.Comment = "お気に入り曲のBOXです。";
			return favoriteBox;
		}

		private static void tAddBackBox( CSongListNode favoriteBox )
		{
			CSongListNode itemBack = new CSongListNode();
			itemBack.eNodeType = CSongListNode.ENodeType.BACKBOX;
			itemBack.strタイトル = "<< BACK";
			itemBack.nスコア数 = 1;
			itemBack.r親ノード = favoriteBox;
			itemBack.strBreadcrumbs = favoriteBox.strBreadcrumbs + " > " + itemBack.strタイトル;
			itemBack.arScore[ 0 ] = new CScore();
			itemBack.arScore[ 0 ].SongInformation.Title = itemBack.strタイトル;
			itemBack.arScore[ 0 ].SongInformation.Preimage = CSkin.Path( @"Graphics\5_preimage backbox.png" );
			itemBack.arScore[ 0 ].SongInformation.Comment = "BOX を出ます。";
			favoriteBox.list子リスト.Insert( 0, itemBack );
		}

		private static void tRemoveExistingFavoritesBox( List<CSongListNode> list )
		{
			for( int i = list.Count - 1; i >= 0; i-- )
			{
				if( list[ i ] != null && list[ i ].eNodeType == CSongListNode.ENodeType.BOX && list[ i ].strタイトル == FavoriteBoxTitle )
					list.RemoveAt( i );
			}
		}

		private static HashSet<string> tLoadFavoriteKeys()
		{
			HashSet<string> favoriteKeys = new HashSet<string>( StringComparer.InvariantCultureIgnoreCase );
			string path = FavoritesFilePath;
			if( !File.Exists( path ) )
				return favoriteKeys;

			foreach( string rawLine in File.ReadAllLines( path, Encoding.GetEncoding( "shift-jis" ) ) )
			{
				string line = rawLine.Trim();
				if( line.Length == 0 || line.StartsWith( ";" ) )
					continue;

				int commentIndex = line.IndexOf( ';' );
				if( commentIndex >= 0 )
					line = line.Substring( 0, commentIndex ).Trim();

				if( line.StartsWith( FavoriteEntryPrefix, StringComparison.OrdinalIgnoreCase ) )
					line = line.Substring( FavoriteEntryPrefix.Length ).TrimStart( ':', ' ', '\t' );

				if( line.StartsWith( "setdef:", StringComparison.OrdinalIgnoreCase ) || line.StartsWith( "score:", StringComparison.OrdinalIgnoreCase ) )
					favoriteKeys.Add( tNormalizeKey( line ) );
			}

			return favoriteKeys;
		}

		private static void tSaveFavoriteKeys( HashSet<string> favoriteKeys )
		{
			string path = FavoritesFilePath;
			string directory = Path.GetDirectoryName( path );
			if( !string.IsNullOrEmpty( directory ) && !Directory.Exists( directory ) )
				Directory.CreateDirectory( directory );

			List<string> keys = new List<string>( favoriteKeys );
			keys.Sort( StringComparer.InvariantCultureIgnoreCase );

			using( StreamWriter writer = new StreamWriter( path, false, Encoding.GetEncoding( "shift-jis" ) ) )
			{
				writer.WriteLine( "#TITLE: " + FavoriteBoxTitle );
				writer.WriteLine();
				foreach( string key in keys )
					writer.WriteLine( FavoriteEntryPrefix + ": " + key );
			}
		}

		private static string tMakeRelativeToDtxPath( string absolutePath )
		{
			if( string.IsNullOrEmpty( absolutePath ) )
				return "";

			string fullPath = Path.GetFullPath( absolutePath );
			foreach( string basePath in tGetDtxSearchPaths() )
			{
				if( fullPath.StartsWith( basePath, StringComparison.InvariantCultureIgnoreCase ) )
					return fullPath.Substring( basePath.Length ).Replace( Path.DirectorySeparatorChar, '/' );
			}

			return fullPath.Replace( Path.DirectorySeparatorChar, '/' );
		}

		private static IEnumerable<string> tGetDtxSearchPaths()
		{
			if( string.IsNullOrEmpty( CDTXMania.ConfigIni.str曲データ検索パス ) )
				yield break;

			string[] paths = CDTXMania.ConfigIni.str曲データ検索パス.Split( new char[] { ';' } );
			foreach( string rawPath in paths )
			{
				if( string.IsNullOrWhiteSpace( rawPath ) )
					continue;

				string path = rawPath;
				if( !Path.IsPathRooted( path ) )
					path = Path.Combine( CDTXMania.strEXEのあるフォルダ, path );

				path = Path.GetFullPath( path );
				if( !path.EndsWith( Path.DirectorySeparatorChar.ToString() ) )
					path += Path.DirectorySeparatorChar;

				yield return path;
			}
		}

		private static CScore tGetFirstAvailableScore( CSongListNode song )
		{
			if( song == null )
				return null;

			for( int i = 0; i < song.arScore.Length; i++ )
			{
				if( song.arScore[ i ] != null )
					return song.arScore[ i ];
			}

			return null;
		}

		private static string tNormalizeKey( string key )
		{
			return ( key ?? "" ).Replace( '\\', '/' ).Trim().ToLowerInvariant();
		}
	}
}

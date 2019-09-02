﻿using System;
using System.Linq;
using System.Collections.Generic;

namespace Nine.Core
{
	public class Board : IUpdatable
	{
		public const int ROW_WIDTH = 6;
		public const int COLUMN_HEIGHT = 13;
		public float ScrollSpeed { get; private set; }
		public float ScrollProgress;
		public uint Score { get; private set; } = 0;
		public int BlocksCleared { get; private set; } = 0;

		private readonly Random rand = new Random();
		
		// Blocks are accessed by Blocks[y][x]
		public Block[][] Blocks { get; set; }
		public bool GameOver
		{
			get
			{
				return Blocks[COLUMN_HEIGHT - 1].Any(block => block != null);
			}
		}

		public int StackHeight
		{
			get
			{
				for(int y = COLUMN_HEIGHT - 1; y < COLUMN_HEIGHT; y--)
				{
					if (Blocks[y].Any((block) => block != null))
					{
						return y;
					}
				}
				return 0;
			}
		}

		public Board(float scrollSpeed) //Constructor
		{
			this.ScrollSpeed = scrollSpeed;
			this.Initialize();
			processMatches();
		}

		public void Initialize()
		{
			Blocks = new Block[COLUMN_HEIGHT][];

			for(int row = 0; row < Blocks.Length; row++)
			{
				if (row < Blocks.Length / 2)
				{
					Blocks[row] = GetNewRow(row);
				}
				else
				{
					Blocks[row] = new Block[ROW_WIDTH];
				}

				foreach(var block in Blocks[row])
				{
					if (block != null)
					{
						block.SetSwappable(block.Position.Y > 0);
					}
				}
			}

			processMatches();
			Score = 0;
			BlocksCleared = 0;
		}

		public void ShiftBlocksUp()
		{
			ScrollProgress--;

			// shift UP
			for (int row = Blocks.Length - 1; row >= 0; row--)
			{
				// replace bottom row with some fresh blocks
				if(row == 0)
				{
					Blocks[row] = GetNewRow(0);
				}
				else
				{
					Blocks[row] = Blocks[row - 1]; // TODO: ShiftRowUp(row) ?
					for (int x = 0; x < ROW_WIDTH; x++)
					{
						if (Blocks[row][x] != null)
						{
							Blocks[row][x].Position = (Blocks[row][x].Position.X, row);

							if(row == 1)
							{
								Blocks[row][x].SetSwappable(true);
							}
						}
					}
				}
			}

			processMatches();
		}

		public Block[] GetNewRow(int y)
		{
			var row = new Block[ROW_WIDTH];
			BlockType? lastLastType = null;
			BlockType? lastType = null;

			for (int i = 0; i < row.Length; i++)
			{
				BlockType? thisType = null;

				while (thisType == null || (lastType != null && lastLastType != null && thisType == lastType && thisType == lastLastType))
				{
					thisType = (BlockType)rand.Next(Enum.GetNames(typeof(BlockType)).Length - 1); //remember to add colors before garbage in the enum
				}

				lastLastType = lastType;
				lastType = thisType;

				row[i] = new Block(i, y, thisType.Value);
			}

			return row;
		}

		private void swapBlocks((int X, int Y) pointA, (int X, int Y) pointB)
		{
			var blockA = Blocks[pointA.Y][pointA.X];
			var blockB = Blocks[pointB.Y][pointB.X];

			// TODO: account for animation/state transition time
			// maybe tell the blocks that they are in the swapping state, and where to?
			if (blockA != null)
			{
				blockA.Position = pointB;
			}
			if (blockB != null)
			{
				blockB.Position = pointA;
			}

			Blocks[pointA.Y][pointA.X] = blockB;
			Blocks[pointB.Y][pointB.X] = blockA;
		}

		public void Swap((int X, int Y) pointA, (int X, int Y) pointB)
		{
			swapBlocks(pointA, pointB);
			processMatches();
			fillGaps();
		}

		public void Update(float deltaTime)
		{
			ScrollProgress += deltaTime * ScrollSpeed;
		}

		public Block GetBlock((int X, int Y) position)
		{
			return Blocks[position.Y][position.X];
		}

		Block GetBlock(int x, int y)
		{
			if(isInPlayableBoard(x, y))
			{
				return Blocks[y][x];
			}

			return null;
		}

		private bool isInPlayableBoard(int x, int y)
		{
			return (x >= 0 && y >= 1 && x < ROW_WIDTH && y < COLUMN_HEIGHT);
		}

		private void fillGap(int x, int y)
		{
			swapBlocks((x, y), (x, y + 1));
		}

		private void fillGaps()
		{
			bool isBlockThatCanFall = true;

			while (isBlockThatCanFall)
			{
				isBlockThatCanFall = false;

				for (int y = 0; y < COLUMN_HEIGHT - 1; y++)
				{
					for (int x = 0; x < ROW_WIDTH; x++)
					{
						if (Blocks[y][x] == null)
						{
							fillGap(x, y);
						}

						if (Blocks[y][x] != null && isInPlayableBoard(x, y - 1) && Blocks[y - 1][x] == null)
						{
							isBlockThatCanFall = true;
						}
					}
				}
			}
			processMatches();
		}

		private void removeBlock(Block block)
		{
			Blocks[block.Position.Y][block.Position.X] = null;
			fillGaps();
			BlocksCleared++;

			adjustScrollSpeed();
		}

		private void adjustScrollSpeed()
		{
			ScrollSpeed = 1/5f;

			if (BlocksCleared > 10)
			{
				ScrollSpeed = 2/9f;
			}
			if (BlocksCleared > 30)
			{
				ScrollSpeed = 1/4f;
			}
			if (BlocksCleared > 60)
			{
				ScrollSpeed = 2/7f;
			}
			if (BlocksCleared > 120)
			{
				ScrollSpeed = 1/3f;
			}
			if (BlocksCleared > 240)
			{
				ScrollSpeed = 2/5f;
			}
		}

		private void processMatches()
		{
			// do whatever we want to do with matches -- delete blocks for now
			var matchBlocks = GetMatchBlocks();
			var blocksToClear = matchBlocks.Count();

			if (blocksToClear > 0)
			{
				uint blockScore = (uint)(blocksToClear - 2);

				Score += (blockScore * blockScore) * 100U;
			}

			foreach(var block in matchBlocks)
			{
				removeBlock(block);
			}
		}

		IEnumerable<Block> GetMatchBlocks()
		{
			var matchBlocks = new List<Block>();

			for (int y = 1; y < COLUMN_HEIGHT; y++)
			{
				for (int x = 0; x < ROW_WIDTH; x++)
				{
					var thisBlock = GetBlock(x, y);

					if(thisBlock == null)
					{
						continue;
					}

					var leftBlock = GetBlock(x - 1, y);
					var rightBlock = GetBlock(x + 1, y);
					var aboveBlock = GetBlock(x, y + 1);
					var belowBlock = GetBlock(x, y - 1);

					if (leftBlock != null && rightBlock != null && thisBlock.Type == leftBlock.Type && thisBlock.Type == rightBlock.Type)
					{
						// horizontal match
						// TODO: flag blocks as inactive/able to be deleted by typing

						// for now: flag them for deletion
						matchBlocks.Add(leftBlock);
						matchBlocks.Add(thisBlock);
						matchBlocks.Add(rightBlock);
					}

					if (aboveBlock != null && belowBlock != null && thisBlock.Type == aboveBlock.Type && thisBlock.Type == belowBlock.Type)
					{
						// vertical match
						// TODO: flag blocks as inactive/able to be deleted by typing

						// for now: flag them for deletion
						matchBlocks.Add(aboveBlock);
						matchBlocks.Add(thisBlock);
						matchBlocks.Add(belowBlock);
					}
				}
			}

			return matchBlocks.Distinct();
		}
	}
}
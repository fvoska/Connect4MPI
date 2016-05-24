using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace C4_Game
{
    /// <summary>
    /// StateNode class represents game state at some moment.
    /// It is structured and can have references to previous state
    /// and to all possible states that current state can transform into by making valid moves.
    /// </summary>
    [Serializable]
    public class StateNode : ICloneable
    {
        // Stores current board state
        public GameState BoardState { get; set; }

        // List of all possible valid moves.
        public List<StateNode> PossibleMoves { get; private set; }

        // Link to previos board state.
        public StateNode PreviousBoardState { get; set; }

        // Indicated whose turn to play it is.
        public byte WhoseTurn { get; set; }

        // Win/Loss state
        public int State;

        // Score of this subtree.
        public double Score;

        // Number of wins/losses/draws in this subtree.
        public int WinCount = 0;
        public int LossCount = 0;
        public int NoEndCount = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="boardState">Board state of this node.</param>
        public StateNode(GameState boardState)
        {
            BoardState = boardState;
            WhoseTurn = (BoardState.LastPlayer == 1) ? (byte)2 : (byte)1;
            PreviousBoardState = null;
            
            Score = 0;
            State = -2;
            PossibleMoves = new List<StateNode>();
        }

        /// <summary>
        /// Checks current state and updates state, score and win/loss count.
        /// </summary>
        /// <returns></returns>
        public byte UpdateState()
        {
            byte winner = BoardState.CheckState();
            if (winner == 0)
            {
                // There was no winning move made last turn.
                State = 0;
                Score = 0;
                NoEndCount++;
            }
            else if (winner == 1)
            {
                // On his last turn, player made a winning move.
                State = -1;
                Score = -1;
                LossCount++;
            }
            else if (winner == 2)
            {
                // On his last turn, CPU made a winning move.
                State = 1;
                Score = 1;
                WinCount++;
            }
            return winner;
        }

        /// <summary>
        /// Generates all possible moves from current state.
        /// </summary>
        public void GeneratePossibleMoves()
        {
            // Try each possible move.
            for (int i = 0; i < BoardState.Width; i++)
            {
                // Check if column has space for insertion.
                if (BoardState.IsValidMove(i))
                {
                    // Clone current state.
                    GameState newState = (GameState)BoardState.Clone();

                    // Insert new token.
                    newState.Insert(i, WhoseTurn);

                    // Create new node and add it as a child.
                    StateNode newNode = new StateNode(newState);
                    newNode.PreviousBoardState = this;
                    PossibleMoves.Add(newNode);
                }
            }
        }

        /// <summary>
        /// Calculate maximum tree depth.
        /// </summary>
        /// <returns>Maximum tree depth.</returns>
        public int TreeDepth()
        {
            if (this.PossibleMoves.Count == 0)
            {
                return 0;
            }
            else
            {
                List<int> possibleDepths = new List<int>();
                foreach(StateNode possibleMove in this.PossibleMoves)
                {
                    possibleDepths.Add(possibleMove.TreeDepth());
                }
                return possibleDepths.Max() + 1;
            }
        }

        /// <summary>
        /// Creates a clone of object upon which it is called.
        /// Implementation of ICloneable.
        /// </summary>
        /// <returns>Clone of current node.</returns>
        public object Clone()
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, this);
                ms.Position = 0;

                return (StateNode)formatter.Deserialize(ms);
            }
        }
    }
}

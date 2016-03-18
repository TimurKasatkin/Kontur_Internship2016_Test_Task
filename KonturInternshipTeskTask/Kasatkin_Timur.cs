using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KonturInternshipTeskTask
{
    public class Hanabi
    {
        private Game currentGame;

        private bool CurrentGameOverOrNotExists => currentGame?.Over ?? true;

        //TODO complete

        #region turn' description regular expressions and appropriate actions

        private static bool OverOrNotExists(Game game)
        {
            return game?.Over ?? true;
        }

        #region start new game 

        static readonly Regex _startGameCommandRegex =
            new Regex("^Start new game with deck (?<deck>((?<card>[RGBWY][1-5]) ?){11,})$");

        static Game StartGame(string turnDescription, Game currentGame)
        {
            string[] cardDescriptions =
                _startGameCommandRegex.Match(turnDescription).Groups["deck"].Value.Split();
            var player1Cards = cardDescriptions.Take(5).Select(CardUtils.ParseCard).ToList();
            var player2Cards = cardDescriptions.Skip(5).Take(5).Select(CardUtils.ParseCard).ToList();
            var deck = cardDescriptions.Skip(10).Select(CardUtils.ParseCard).ToList();
            return new Game(new Player(player1Cards), new Player(player2Cards), deck);
        }

        #endregion

        #region play card

        static readonly Regex playCardCommandRegex = new Regex(@"Play card (?<card_index>\d+)");

        static Game PlayCard(string turnDescription, Game currentGame)
        {
            if (!OverOrNotExists(currentGame))
            {
                var cardIndex = ushort.Parse(playCardCommandRegex.Match(turnDescription).Groups["card_index"].Value);
                currentGame.PlayCard(cardIndex);
            }
            return currentGame;
        }

        #endregion

        #region drop card

        static readonly Regex dropCardCommandRegex = new Regex(@"Drop card (?<card_index>\d+)");

        static Game DropCard(string turnDescription, Game currentGame)
        {
            if (!OverOrNotExists(currentGame))
            {
                var cardIndex = ushort.Parse(playCardCommandRegex.Match(turnDescription).Groups["card_index"].Value);
                currentGame.DropCard(cardIndex);
            }
            return currentGame;
        }

        #endregion

        // TODO implement tell color action

        #region tell color

        static readonly Regex tellColorCommandRegex =
            new Regex(@"Tell color (Red|Green|Blue|White|Yellow) for cards (?<card_indexes>((?<card_index>\d+) ?)+)");

        #endregion

        // TODO implement tell rank action

        #region tell rank

        static readonly Regex tellRankCommandRegex =
            new Regex(@"Tell rank [1-5] for cards (?<card_indexes>((?<card_index>\d+) ?)+)");

        #endregion

        #region connection between turn description and actual action

        private Dictionary<Regex, Func<string, Game, Game>> _commandActionDict = new Dictionary
            <Regex, Func<string, Game, Game>>
        {
            {_startGameCommandRegex, StartGame},
            {playCardCommandRegex, PlayCard}
        };

        private Func<string, Game, Game> GetTurnBy(string turnDescription)
        {
            return _commandActionDict
                .FirstOrDefault(turnRegexActionPair => turnRegexActionPair.Key.IsMatch(turnDescription ?? ""))
                .Value;
        }

        #endregion

        #endregion

        public void Play()
        {
            while (true)
            {
                var turnDescription = Console.ReadLine();
                currentGame = GetTurnBy(turnDescription)?.Invoke(turnDescription, currentGame);
                if (CurrentGameOverOrNotExists)
                {
                    Console.WriteLine("Turn: {0}, cards: {1}, with risk: {2}",
                        currentGame.NumberOfTurns, currentGame.NumOfCorrectlyPlayedCards, currentGame.NumOfRiskyTurns);
                    currentGame = null;
                }
            }
        }

        public static void Main(string[] args)
        {
            new Hanabi().Play();
        }
    }

    internal class Game
    {
        public uint NumberOfTurns { get; private set; } = 0;

        public uint NumOfCorrectlyPlayedCards { get; private set; } = 0;

        public uint NumOfRiskyTurns { get; private set; } = 0;

        public bool Over { get; /*{ return Over || _trash.Count >= 25 || _deck.Count == 0; }*/
            private set; } = false;

        private List<Card> _trash = new List<Card>();
        private List<Card> _deck;

        private readonly Player _player1, _player2;
        private Player _curPlayer;

        public Game(Player player1, Player player2, List<Card> deck)
        {
            if (player1 == null || player2 == null)
            {
                throw new ArgumentException("You should pass players!");
            }
            if (deck == null)
            {
                throw new ArgumentException("You should pass cards which remaining in deck!");
            }
            _deck = deck;
            _player1 = player1;
            _player2 = player2;
            _curPlayer = player1;
        }

        public void PlayCard(ushort cardIndex)
        {
            ChangeCurrentPlayer();
        }

        public void DropCard(ushort cardIndex)
        {
            ChangeCurrentPlayer();
        }

        private void ChangeCurrentPlayer()
        {
            _curPlayer = _curPlayer == _player1 ? _player2 : _player1;
        }
    }

    internal class Player
    {
        public List<Card> Cards { get; }

        public Player(List<Card> cards)
        {
            if (cards == null || cards.Count < 5)
                throw new ArgumentException("Invalid player's cards");
            Cards = cards;
        }

        public void AddCard(Card card)
        {
            Cards.Add(card);
        }
    }

    internal class Card
    {
        public Card(CardColor color, ushort rank)
        {
            this.Color = color;
            this.Rank = rank;
        }

        public readonly CardColor Color;

        public readonly ushort Rank;
    }

    internal enum CardColor
    {
        Red,
        Green,
        Blue,
        Yellow,
        White
    }

    internal static class CardUtils
    {
        public static CardColor? ParseColor(this char cardColorMarker)
        {
            switch (cardColorMarker)
            {
                case 'R':
                    return CardColor.Red;
                case 'G':
                    return CardColor.Green;
                case 'B':
                    return CardColor.Blue;
                case 'Y':
                    return CardColor.Yellow;
                case 'W':
                    return CardColor.White;
                default:
                    return null;
            }
        }

        public static Card ParseCard(this string cardDescription)
        {
            if (cardDescription == null || cardDescription.Length != 2)
            {
                return null;
            }
            var cardColor = cardDescription[0].ParseColor();
            if (!cardColor.HasValue)
            {
                return null;
            }
            var cardRank = (ushort) char.GetNumericValue(cardDescription[1]);
            if (!(1 <= cardRank && cardRank <= 5))
            {
                return null;
            }
            return new Card(cardColor.Value, cardRank);
        }
    }
}
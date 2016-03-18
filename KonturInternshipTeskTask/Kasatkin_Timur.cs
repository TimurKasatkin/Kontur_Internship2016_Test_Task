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
            new Regex(
                @"Tell color (?<color>Red|Green|Blue|White|Yellow) for cards (?<card_indexes>((?<card_index>\d+) ?)+)");

        static Game TellColor(string turnDescription, Game currentGame)
        {
            if (!OverOrNotExists(currentGame))
            {
                var match = tellColorCommandRegex.Match(turnDescription);
                var proposedColor = match.Groups["color"].Value.ParseColor().Value;
                var cardIndexes = match.Groups["card_indexes"].Value.Split()
                    .Select(ushort.Parse);
                currentGame.TellColor(proposedColor, cardIndexes);
            }
            return currentGame;
        }

        #endregion

        // TODO implement tell rank action

        #region tell rank

        static readonly Regex tellRankCommandRegex =
            new Regex(@"Tell rank (?<card_rank>[1-5]) for cards (?<card_indexes>(\d+ ?)+)");

        static Game TellRank(string turnDescription, Game currentGame)
        {
            if (!OverOrNotExists(currentGame))
            {
                var match = tellRankCommandRegex.Match(turnDescription);
                var proposedRank = match.Groups["card_rank"].Value.ParseRank();
                var cardIndexes = match.Groups["card_indexes"].Value.Split()
                    .Select(ushort.Parse);
                currentGame.TellRank(proposedRank, cardIndexes);
            }
            return currentGame;
        }

        #endregion

        #region connection between turn description and actual action

        private readonly Dictionary<Regex, Func<string, Game, Game>> _commandActionDict = new Dictionary
            <Regex, Func<string, Game, Game>>
        {
            {_startGameCommandRegex, StartGame},
            {playCardCommandRegex, PlayCard},
            {tellColorCommandRegex, TellColor},
            {tellRankCommandRegex, TellRank}
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
                currentGame = GetTurnBy(turnDescription)?.Invoke(turnDescription, currentGame) ?? currentGame;
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

        #region players information

        private readonly Player _player1, _player2;

        private Player _curPlayer;

        private Player _otherPlayer
        {
            get { return _curPlayer == _player1 ? _player2 : _player1; }
        }

        #endregion

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
            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        public void DropCard(ushort cardIndex)
        {
            _trash.Add(_curPlayer.PopCard(cardIndex));
            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        //TODO change for risky turns
        public void TellColor(CardColor proposedColor, IEnumerable<ushort> cardIndexes)
        {
            bool tipIsValid = cardIndexes
                .Select(_otherPlayer.GetCard)
                .Select(card => card.Color)
                .All(color => color.Equals(proposedColor));
            if (!tipIsValid)
            {
                Over = true;
            }
            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        public void TellRank(ushort proposedRank, IEnumerable<ushort> cardIndexes)
        {
            bool tipIsValid = cardIndexes
                .Select(_otherPlayer.GetCard)
                .Select(card => card.Rank)
                .All(rank => rank == proposedRank);
            if (!tipIsValid)
            {
                Over = true;
            }
            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        private void ChangeCurrentPlayerAndIncNumOfTurns()
        {
            ChangeCurrentPlayer();
            NumberOfTurns++;
        }

        private void ChangeCurrentPlayer()
        {
            _curPlayer = _curPlayer == _player1 ? _player2 : _player1;
        }
    }

    internal class Player
    {

        //TODO add two list for info about cards(ranks and colors): based on this we can say whether person certain about his move or not (risky moves)

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

        public Card GetCard(ushort cardIndex)
        {
            return Cards[cardIndex];
        }

        public Card PopCard(ushort cardIndex)
        {
            var card = Cards[cardIndex];
            Cards.RemoveAt(cardIndex);
            return card;
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
        public static CardColor? ParseColor(this string cardColorName)
        {
            switch (cardColorName)
            {
                case "Red":
                    return CardColor.Red;
                case "Green":
                    return CardColor.Green;
                case "Blue":
                    return CardColor.Blue;
                case "Yellow":
                    return CardColor.Yellow;
                case "White":
                    return CardColor.White;
                default:
                    return null;
            }
        }

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

        public static ushort ParseRank(this string cardRankString)
        {
            ushort rank = 0;
            ushort.TryParse(cardRankString, out rank);
            return rank;
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
module Hedgehog.Tests.MinimalTests

open Hedgehog
open Hedgehog.Gen.Operators
open TestDsl

type Exp =
    | Lit of int
    | Var of string
    | Lam of string * Exp
    | App of Exp * Exp

let shrinkExp : Exp -> List<Exp> = function
    | Lam (_, x) ->
        [x]
    | App (x, y) ->
        [x; y]
    | _ ->
        []

// This will not be initialized if using version <= 15.7.0 of Microsoft.NET.Test.SDK
let genName =
    Gen.item ["a"; "b"; "c"; "d"]

// a vaguely interesting predicate which checks that a certain sub-expression
// cannot be found anywhere in the expression.
let rec noAppLit10 : Exp -> bool = function
    | Lit _ ->
        true
    | Var _ ->
        true
    | Lam (_, x) ->
        noAppLit10 x
    | App (_, Lit 10) ->
        false
    | App (x, y) ->
        noAppLit10 x &&
        noAppLit10 y

// FIXME does this exist in the core libraries already?
let (<|>) x y =
    match x with
    | None ->
        y
    | Some _ ->
        x

let rec tryFindSmallest (p : 'a -> bool) (Node (x, xs) : Tree<'a>) : 'a option =
    if p x then
        None
    else
        Seq.tryPick (tryFindSmallest p) xs <|> Some x

#nowarn "40"
// This will not be initialized if using version <= 15.7.0 of Microsoft.NET.Test.SDK
let rec genExp : Gen<Exp> =
    Gen.delay (fun _ ->
        let recs = [
            Lit <!> Gen.int32 (Range.constant 0 10)
            Var <!> genName
        ]

        let nonrecs = [
            Lam <!> Gen.zip genName genExp
            App <!> Gen.zip genExp genExp
        ]

        Gen.choiceRec recs nonrecs
        |> Gen.shrink shrinkExp // comment this out to see the property fail
    )

let perfectMinimalShrink () =
    Property.check (property {
        let! xs = Gen.mapTree Tree.duplicate genExp |> Gen.resize 20
        match tryFindSmallest noAppLit10 xs with
        | None ->
            return true
        | Some (App (Lit 0, Lit 10)) ->
            return true
        | Some x ->
            return! property {
                counterexample ""
                counterexample "Greedy traversal with predicate did not yield the minimal shrink."
                counterexample ""
                counterexample "=== Minimal ==="
                counterexample (sprintf "%A" (App (Lit 0, Lit 10)))
                counterexample "=== Actual ==="
                counterexample (sprintf "%A" x)
                return false
            }
    })

let minimalTests = testList "Minimal tests" [
    testCase "greedy traversal with a predicate yields the perfect minimal shrink" perfectMinimalShrink
]

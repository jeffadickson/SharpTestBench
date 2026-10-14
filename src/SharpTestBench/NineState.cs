// Logic truth tables ported from cocotb. Copyright cocotb contributors.
// SPDX-License-Identifier: BSD-3-Clause. See third_party/COCOTB-LICENSE.
namespace SharpTestBench;
public static class NineState
{
    static readonly string[] AndTable = ["UU0UUU0UU", "UX0XXX0XX", "000000000", "UX01XX01X", "UX0XXX0XX", "UX0XXX0XX", "000000000", "UX01XX01X", "UX0XXX0XX"];
    public static Logic And(Logic a, Logic b) => LogicOps.Parse(AndTable[(int)a][(int)b]);
    static readonly string[] OrTable = ["UUU1UUU1U", "UXX1XXX1X", "UX01XX01X", "111111111", "UXX1XXX1X", "UXX1XXX1X", "UX01XX01X", "111111111", "UXX1XXX1X"];
    public static Logic Or(Logic a, Logic b) => LogicOps.Parse(OrTable[(int)a][(int)b]);
    static readonly string[] XorTable = ["UUUUUUUUU", "UXXXXXXXX", "UX01XX01X", "UX10XX10X", "UXXXXXXXX", "UXXXXXXXX", "UX01XX01X", "UX10XX10X", "UXXXXXXXX"];
    public static Logic Xor(Logic a, Logic b) => LogicOps.Parse(XorTable[(int)a][(int)b]);
    public static Logic Not(Logic a) => LogicOps.Parse("UX10XX10X"[(int)a]);
}

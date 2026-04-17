import { formatApiProblemMessage, parseApiProblem } from "./api-problem";

describe("parseApiProblem", () => {
  it("returns null for non-object bodies", () => {
    expect(parseApiProblem(null)).toBeNull();
    expect(parseApiProblem(undefined)).toBeNull();
    expect(parseApiProblem("text")).toBeNull();
  });

  it("returns null when no title, detail, or correlationId", () => {
    expect(parseApiProblem({ status: 400 })).toBeNull();
  });

  it("parses Problem Details fields", () => {
    const p = parseApiProblem({
      type: "about:blank",
      title: "Error",
      status: 403,
      detail: "Más información",
      correlationId: "abc-123"
    });
    expect(p).not.toBeNull();
    expect(p!.title).toBe("Error");
    expect(p!.status).toBe(403);
    expect(p!.detail).toBe("Más información");
    expect(p!.correlationId).toBe("abc-123");
  });

  it("accepts correlationId-only bodies", () => {
    const p = parseApiProblem({ correlationId: "ref-only" });
    expect(p!.correlationId).toBe("ref-only");
  });
});

describe("formatApiProblemMessage", () => {
  it("joins title and detail", () => {
    expect(
      formatApiProblemMessage({
        title: "T",
        detail: "D"
      })
    ).toBe("T — D");
  });

  it("deduplicates when title equals detail (ApiProblemBody fallback)", () => {
    expect(
      formatApiProblemMessage({
        title: "Mismo",
        detail: "Mismo"
      })
    ).toBe("Mismo");
  });

  it("appends correlation ref", () => {
    expect(
      formatApiProblemMessage({
        title: "X",
        correlationId: "g-1"
      })
    ).toBe("X Ref: g-1.");
  });
});

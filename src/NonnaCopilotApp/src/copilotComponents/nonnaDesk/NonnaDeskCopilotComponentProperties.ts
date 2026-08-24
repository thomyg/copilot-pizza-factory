import { z } from 'zod';
import zodToJsonSchema from 'zod-to-json-schema';

const propertiesSchema = z.object({
  view: z
    .enum(['rota', 'approvals', 'invoices', 'timeoff'])
    .describe(
      "Which desk view to spotlight: 'rota' for the shift plan (who works when, open seats, sick calls), " +
        "'approvals' for requisitions waiting for a signature and this month's budget position, " +
        "'timeoff' for absence requests with the cover already worked out, " +
        "'invoices' for the supplier invoice ledger and spending."
    ),
  nonnaSays: z
    .string()
    .optional()
    .describe(
      "A short personal remark from Nonna (one short sentence, warm but strict, e.g. 'Approve it or the Hawaii dies tonight, caro.') " +
        'shown as her handwritten note on the desk.'
    )
});

export type INonnaDeskCopilotComponentProperties = z.infer<typeof propertiesSchema>;

export default zodToJsonSchema(propertiesSchema);

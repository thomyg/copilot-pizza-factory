import { z } from 'zod';
import zodToJsonSchema from 'zod-to-json-schema';

const propertiesSchema = z.object({
  view: z
    .enum(['tonight', 'report', 'forecast', 'preorders'])
    .describe(
      "Which cockpit view to spotlight: 'tonight' for the current service status (tables, kitchen, stock, satisfaction), " +
        "'report' for today's business numbers and 7-day history, 'forecast' for the crystal-ball risk outlook " +
        "(what will most likely be a problem soon), 'preorders' for the reservation book of upcoming pre-orders and parties."
    ),
  giuseppeSays: z
    .string()
    .optional()
    .describe(
      "A short personal remark from Giuseppe (one short sentence, warm and in character, e.g. 'The oven hums, the derby crowd looms — andiamo!') " +
        'shown as his handwritten note on the cockpit.'
    )
});

export type ITrattoriaCommandCopilotComponentProperties = z.infer<typeof propertiesSchema>;

export default zodToJsonSchema(propertiesSchema);
